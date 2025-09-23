import type { BodyId, BodyState, SimState, StateData, DiffData } from "@bridge";

export class StateManager {
    #simReader: SimStateReader;
    #bodiesReader: BodiesStateReader;

    #getPointerData: () => [simBufferPtr: number, simBufferSizeInBytes: number, bodyBufferPtr: number, bodyBufferSizeInBytes: number];

    readonly state: Readonly<StateData>;
    readonly diff: Readonly<DiffData>;

    /**
     * @param layouts       String arrays with the ordered keys for SimState and BodyState as exported by C# WASM interface during initialization.
     * @param injections    Injected functions. 
     */
    constructor(
        layouts: { sim: string[], body: string[] },
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

        const simReader = new SimStateReader(new BufferViewHandler(heapViewGetter, log), layouts.sim);
        const bodiesReader = new BodiesStateReader(new BufferViewHandler(heapViewGetter, log), layouts.body);

        this.#simReader = simReader;
        this.#bodiesReader = bodiesReader;

        this.state = {
            get sim() { return simReader.state; },
            get bodies() { return bodiesReader.state; }
        }

        this.diff = {
            get sim() { return simReader.diff; },
            get bodies() { return bodiesReader.diff; }
        };
    }

    refresh() : void {
        const [simPtr, simSize, bodyPtr, bodySize] = this.#getPointerData();
        this.#simReader.refresh(simPtr, simSize);
        this.#bodiesReader.refresh(bodyPtr, bodySize, this.state.sim.bodyCount);
    };
}

abstract class StateReader<T extends SimState | BodyState> {
    protected readonly _layout: StateLayoutHandler<T>;
    protected readonly _viewHandler: BufferViewHandler;

    constructor(
        viewHandler: BufferViewHandler,
        layoutKeyArray: string[],
    ) {
        this._viewHandler = viewHandler;
        this._layout = new StateLayoutHandler<T>(layoutKeyArray);
    }

    get layout(): StateLayoutHandler<T> { return this._layout; }

    abstract refresh(...args: any[]): void;
}

class SimStateReader extends StateReader<SimState> {
    #state: SimState = { ...this._layout.template };
    #diff: Set<keyof SimState> = new Set();

    get state(): StateData["sim"] { return this.#state; }
    get diff(): DiffData["sim"] { return this.#diff; }

    refresh(ptr: number, size: number): void {
        this.#diff.clear();
        const view = this._viewHandler.getView(ptr, size);

        for(const [key, index] of this._layout.keyIndexTuples) {
            let value = view[index];
            if(this.#state[key] === value) continue;

            // Since the generation from the LayoutRecords.cs guarantees all
            // properties of T are to be numbers, this won't lead to runtime errors.
            this.#state[key] = value;
            this.#diff.add(key);
        }

        // Handle webkit floating point issue
        // When an int is cast to a double in C# before being written to the
        // shared WASM memory, then read into a Number in javascript,
        // V8 and spidermonkey truncate automatically, making the read value
        // Number.IsInteger(value) == true.
        // Webkit does not, so we have to manually truncate values that must be integers.
        this.#state.bodyCount = Math.trunc(this.#state.bodyCount);
    }
}

class BodiesStateReader extends StateReader<BodyState> {
    #state: StateData["bodies"] = new Map();
    #diff: DiffData["bodies"] = {
        created: new Set(),
        deleted: new Set(),
        updated: new Set()
    }

    #idCache = {
        curr: <Set<BodyId>> new Set(),
        prev: <Set<BodyId>> new Set()
    }

    #bodyStride: number;
    #idIndex: number;

    constructor(...args: ConstructorParameters<typeof StateReader<BodyState>>) {
        super(...args);

        this.#bodyStride = this._layout.keys.size;
        this.#idIndex = this._layout.keyIndexRecord["id"];
    }

    get state() { return this.#state; }
    get diff() { return this.#diff; }

    refresh(ptr: number, size: number, bodyCount: number): void {
        const view = this._viewHandler.getView(ptr, size);

        // Reset diff
        const { created, deleted, updated } = this.#diff;
        created.clear();
        deleted.clear();
        updated.clear();

        // Swap id caches
        [this.#idCache.curr, this.#idCache.prev] = [this.#idCache.prev, this.#idCache.curr];
        this.#idCache.curr.clear();

        // Read buffer data
        for(let i = 0; i < bodyCount; i++) {
            const offset = i * this.#bodyStride;
            const id = view[offset + this.#idIndex];

            this.#idCache.curr.add(id);

            let wasCreated = false;
            let wasUpdated = false;

            let body = this.#state.get(id);
            if(!body) {
                body = { ...this._layout.template };
                this.#state.set(id, body);
                created.add(id);
                wasCreated = true;
            }

            for(const [key, index] of this._layout.keyIndexTuples) {
                const value = view[offset + index];
                if(body[key] === value) continue;

                wasUpdated = true;
                body[key] = value;
            }

            // Handle webkit float issue. See comment in 
            // SimStateReader#refresh() for details. 
            body.id = Math.trunc(body.id);
            body.enabled = Math.trunc(body.enabled);
            body.outOfBounds = Math.trunc(body.outOfBounds);

            if(!wasCreated && wasUpdated) updated.add(id);
        }
        
        // Handle the actual deletion of bodies that were in the previous 
        // frame but are missing from the current frame.
        for(const id of this.#idCache.prev) {
            if(!this.#idCache.curr.has(id)) {
                this.#state.delete(id);
                deleted.add(id);
            }
        }
    }
}

class BufferViewHandler {
    #bufferView?: Float64Array<ArrayBuffer>;
    #ptr?: number;
    #size?: number;

    #heapViewGetter: () => Uint8Array;
    #log?: (msg: any, ...args: any[]) => void;

    constructor(
        heapViewGetter: () => Uint8Array,
        log?: (msg: any, ...args: any[]) => void
    ) {
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

class StateLayoutHandler<T extends SimState | BodyState> {
    /** Tuples of SimState/BodyState property keys and their index offsets in the memory view. */
    readonly #keyIndexTuples: [keyof T, index: number][] = [];
    readonly #keyIndexRecord: Record<keyof T, number>;
    readonly #keys: Set<keyof T> = new Set();
    readonly #template: T;

    constructor(layoutKeyArray: string[]) {
        const stateTemplate = {} as T;
        const keyIndexTuples = <[keyof T, index: number][]>[];
        const keyIndexRecord = <Record<keyof T, number>>{};

        for(let i = 0; i < layoutKeyArray.length; i++) {
            // Because layoutKeyArray and T are generated from the same SSOT,
            // the key is assumed to be valid.
            const key = layoutKeyArray[i] as keyof T;

            this.#keys.add(key);
            keyIndexTuples[i] = [key, i];
            keyIndexRecord[key] = i;
            // Since the generation from the LayoutRecords.cs guarantees all
            // properties of T are to be numbers, this won't lead to runtime errors.
            // `as any` to bypass strict property type checks for this initialization.
            (stateTemplate as any)[key] = 0;
        }

        this.#keyIndexTuples = keyIndexTuples;
        this.#keyIndexRecord = keyIndexRecord;;
        this.#template = stateTemplate;
    }

    get keyIndexTuples(): [keyof T, index: number][] { return this.#keyIndexTuples; }
    get keyIndexRecord(): Record<keyof T, number> { return this.#keyIndexRecord; }
    get template(): T { return this.#template; }
    get keys(): Set<keyof T> { return this.#keys; }
}
