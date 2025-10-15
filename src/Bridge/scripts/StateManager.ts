import type { BodyStateLayout as PhysicsStateBody, SimStateLayout as PhysicsStateSim } from "@LayoutRecords.d.ts";
import { EngineBridgeAPI } from "./DotNetHandler";

// Re-export source code generated types for semantics.
export type { PhysicsStateBody, PhysicsStateSim };

/** Unique ID of a body. */
export type BodyId = PhysicsStateBody["id"];

/**
 * Represents the entire state of the simulation at a given tick.
 * Contains a map of all bodies and other global simulation properties.
 */
export type PhysicsState = {
    readonly sim: PhysicsStateSim,
    readonly bodies: ReadonlyMap<BodyId, PhysicsStateBody>
}

/** Contains information physics engine state changes* during the last engine tick. */
export type PhysicsDiff = {
    /** The keys of SimState that were changed. */
    readonly sim: ReadonlySet<keyof PhysicsStateSim>,
    readonly bodies: {
        /** The ids of newly created bodies. */
        readonly created: ReadonlySet<BodyId>,
        /** The ids of bodies that were updated. */
        readonly deleted: ReadonlySet<BodyId>,
        /** The ids of deleted bodies. */
        readonly updated: ReadonlySet<BodyId>
    }
}

type Flatten<T> = T extends any[] ? T[number] : T;

type CsType<T extends PhysicsStateSim | PhysicsStateBody> = T extends PhysicsStateSim 
    ? Flatten<ReturnType<EngineBridgeAPI["GetSimStateCsTypes"]>> 
    : Flatten<ReturnType<EngineBridgeAPI["GetBodyStateCsTypes"]>>;

type LayoutData<T extends PhysicsStateSim | PhysicsStateBody> = { 
    /** Array of member names of T, in the order in which they appear in memory. */
    keys: (keyof T)[], 
    /** Array of C# type strings of the types of members of T, in the same order as the member names in layoutKeys. */
    csTypes: CsType<T>[] 
}

type LayoutInfo<T extends PhysicsStateSim | PhysicsStateBody> = Readonly<{index: number, key: keyof T, cast: (num: number) => T[keyof T]}>;

const CS_TO_JS_TYPE_CASTS: Record<CsType<PhysicsStateSim> & CsType<PhysicsStateBody>, (input: number) => number | boolean> = {
    Boolean: (input: number) => input >= 0.5,
    /*
        Handles webkit floating point issue.
        When an int is cast to a double in C# before being written to the shared WASM memory, 
        then read into a Number in javascript, V8 and spidermonkey truncate automatically,
        making the read value Number.IsInteger(value) == true.
        Webkit does not, so we have to manually truncate values that must be integers.
    */
    Int32: (input: number) => Math.trunc(input),
    Double: (input: number) => input,
} as const;


export class StateManager {
    #simReader: SimStateReader;
    #bodiesReader: BodiesStateReader;
    #getPointerData: () => [simBufferPtr: number, simBufferSizeInBytes: number, bodyBufferPtr: number, bodyBufferSizeInBytes: number];

    readonly state: Readonly<PhysicsState>;
    readonly diff: Readonly<PhysicsDiff>;

    constructor(layoutData: { sim: LayoutData<PhysicsStateSim>, body: LayoutData<PhysicsStateBody> },
        injections: {
            /** C# WASM function to get the pointers and sizes for the two different shared memory buffers. */
            getPointerData: () => [simBufferPtr: number, simBufferSizeInBytes: number, bodyBufferPtr: number, bodyBufferSizeInBytes: number],
            /** C# WASM function: Returns a short term view of the WASM linear memory. Don't store the reference, don't use it after await.  */
            heapViewGetter: () => Uint8Array,
            /** Simple logger utility only active in debug builds. */
            log?: (msg: any, ...args: any[]) => void
        }
    ) {
        const { getPointerData, heapViewGetter, log } = injections;
        this.#getPointerData = getPointerData;

        const simReader = new SimStateReader(new BufferViewHandler(heapViewGetter, log), layoutData.sim);
        const bodiesReader = new BodiesStateReader(new BufferViewHandler(heapViewGetter, log), layoutData.body);

        this.#simReader = simReader;
        this.#bodiesReader = bodiesReader;

        this.state = { get sim() { return simReader.state; }, get bodies() { return bodiesReader.state; } }
        this.diff = { get sim() { return simReader.diff; }, get bodies() { return bodiesReader.diff; } };
    }

    refresh() : void {
        const [simPtr, simSize, bodyPtr, bodySize] = this.#getPointerData();
        this.#simReader.refresh(simPtr, simSize);
        this.#bodiesReader.refresh(bodyPtr, bodySize, this.state.sim.bodyCount);
    };
}

class BufferViewHandler {
    #bufferView?: Float64Array<ArrayBuffer>;
    #ptr?: number;
    #size?: number;
    #heapViewGetter: () => Uint8Array;
    #log?: (msg: any, ...args: any[]) => void;

    constructor(heapViewGetter: () => Uint8Array, log?: (msg: any, ...args: any[]) => void) {
        this.#heapViewGetter = heapViewGetter;
        this.#log = log;
    }

    getView(ptr: number, size: number): Float64Array<ArrayBuffer> {
        const stale = !this.#bufferView
            || this.#bufferView.buffer.detached 
            || this.#ptr !== ptr
            || this.#size !== size;
        if(stale) this.#refresh(ptr, size);

        return this.#bufferView!;
    }

    #refresh(ptr: number, size: number): void {
        if(ptr === 0) throw new Error(`Invalid buffer pointer=${ptr}`);
        if(size === 0) throw new Error(`Invalid buffer size=${size}`);
        this.#log?.(`Updating BufferView`, {ptr, size});

        // ArrayBuffer is only used in single-threaded context!
        // Using webworkers would require using SharedArrayBuffer instead.
        const heapViewBuffer = this.#heapViewGetter().buffer as ArrayBuffer;
        
        this.#bufferView = new Float64Array<ArrayBuffer>(heapViewBuffer, ptr, size / Float64Array.BYTES_PER_ELEMENT);
        this.#ptr = ptr;
        this.#size = size;
    }
}

abstract class StateReader<T extends PhysicsStateSim | PhysicsStateBody> {
    protected readonly _viewHandler: BufferViewHandler;
    protected readonly _layout: ReadonlyArray<LayoutInfo<T>>;
    protected readonly _template: Readonly<T>;

    constructor(viewHandler: BufferViewHandler, layoutData: LayoutData<T>) {
        const { keys, csTypes } = layoutData;
        if(keys.length !== csTypes.length) throw new Error("Argument arrays must be of the same length.");

        this._viewHandler = viewHandler;
        const layout: { index: number, key: keyof T, cast: (num: number) => T[keyof T] }[] = [];
        const template = {} as T;

        for(let index = 0; index < keys.length; index++) {
            const key = keys[index] as keyof T; // Because keys and T are generated from the same SSOT, the key is assumed to be valid.
            const type = csTypes[index];        // Same holds true for csTypes

            const cast = CS_TO_JS_TYPE_CASTS[type] as (input: number) => T[keyof T];
            if(!cast) throw new Error(`No conversion function defined for type "${type}".`);

            template[key] = cast(0);
            layout[index] = { index, key, cast };
        }

        this._layout = layout;
        this._template = template;
    }

    abstract refresh(ptr: number, size: number, ...args: any[]): void;
}

class SimStateReader extends StateReader<PhysicsStateSim> {
    #state: PhysicsStateSim = { ...this._template };
    #diff: Set<keyof PhysicsStateSim> = new Set();

    get state(): PhysicsState["sim"] { return this.#state; }
    get diff(): PhysicsDiff["sim"] { return this.#diff; }

    refresh(ptr: number, size: number): void {
        const view = this._viewHandler.getView(ptr, size);
        this.#diff.clear();
        
        for(const { index, key, cast } of this._layout) {
            const value = cast(view[index]);

            if(this.#state[key] !== value) {
                this.#state[key] = value;
                this.#diff.add(key);
            }
        }
    }
}

class BodiesStateReader extends StateReader<PhysicsStateBody> {
    #state: Map<number, PhysicsStateBody> = new Map();
    #diff = {
        created: new Set() as Set<BodyId>,
        deleted: new Set() as Set<BodyId>,
        updated: new Set() as Set<BodyId>
    };

    #idLayout = this._layout.find(ele => ele.key === "id")!;
    #restLayout = this._layout.filter(ele => ele.key !== "id");
    #idCache = { curr: new Set() as Set<BodyId>, prev: new Set() as Set<BodyId> };

    get state() { return this.#state; }
    get diff() { return this.#diff; }

    refresh(ptr: number, size: number, bodyCount: number): void {
        const view = this._viewHandler.getView(ptr, size);
        this.#diff.created.clear();
        this.#diff.updated.clear();
        this.#diff.deleted.clear();

        [this.#idCache.curr, this.#idCache.prev] = [this.#idCache.prev, this.#idCache.curr];
        this.#idCache.curr.clear();

        for(let i = 0; i < bodyCount; i++) {
            const offset = i * this._layout.length;
            const id = this.#idLayout.cast(view[offset + this.#idLayout.index]) as PhysicsStateBody["id"];
            this.#idCache.curr.add(id);

            let wasCreated = false;
            let wasUpdated = false;

            let body = this.#state.get(id);
            if(!body) {
                body = { ...this._template, id };
                this.#state.set(id, body);
                wasCreated = true;
            }

            for(const {index, key, cast} of this.#restLayout) { // excludes id
                const value = cast(view[index + offset]);

                if(body[key] !== value) {
                    wasUpdated = true;
                    (body[key] as any) = value;     // Type 'number | boolean' is not assignable to type 'never'. Type 'number' is not assignable to type 'never'.ts(2322)
                }
            }

            if(wasCreated) this.#diff.created.add(id);
            else if(wasUpdated) this.#diff.updated.add(id);
        }
        
        // Handle the actual deletion of bodies that were in the previous frame but are missing from the current frame.
        for(const id of this.#idCache.prev) {
            if(!this.#idCache.curr.has(id)) {
                this.#state.delete(id);
                this.#diff.deleted.add(id);
            }
        }
    }
}