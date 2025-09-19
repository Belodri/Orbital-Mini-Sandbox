import { BodyStateLayout, SimStateLayout } from "../types/LayoutRecords";
export type BodyState = {
    [Property in keyof BodyStateLayout]: BodyStateLayout[Property];
};
export type SimState = {
    [Property in keyof SimStateLayout]: SimStateLayout[Property];
};
export type StateData = {
    sim: SimState;
    bodies: Map<BodyState["id"], BodyState>;
};
export type DiffData = {
    sim: Set<keyof SimState>;
    bodies: {
        created: Set<BodyState["id"]>;
        deleted: Set<BodyState["id"]>;
        updated: Set<BodyState["id"]>;
    };
};
export declare class StateManager {
    #private;
    readonly state: Readonly<StateData>;
    readonly diff: Readonly<DiffData>;
    /**
     * @param layouts       String arrays with the ordered keys for SimState and BodyState as exported by C# WASM interface during initialization.
     * @param injections    Injected functions.
     */
    constructor(layouts: {
        sim: string[];
        body: string[];
    }, injections: {
        /** C# WASM function to get the pointers and sizes for the two different shared memory buffers. */
        getPointerData: () => [simBufferPtr: number, simBufferSizeInBytes: number, bodyBufferPtr: number, bodyBufferSizeInBytes: number];
        /** C# WASM function: Returns a short term view of the WASM linear memory. Don't store the reference, don't use it after await.  */
        heapViewGetter: () => Uint8Array;
        /** Simple logger utility only active in debug builds. */
        log?: (msg: any, ...args: any[]) => void;
    });
    refresh(): void;
}
//# sourceMappingURL=StateManager.d.ts.map