import type { RuntimeAPI } from "@dotnet.d.ts";
import { DotNetHandler, type EngineBridgeAPI } from './DotNetHandler.ts';
import { StateManager, type PhysicsDiff, type PhysicsState, type PhysicsStateSim, type PhysicsStateBody, type BodyId  } from './StateManager.ts';

interface IBridge {
    /** 
     * Snapshot of the most recent physics state, which is updated at the end of every `tickEngine()` call.
     * `undefined` until initialization is complete.
     */
    readonly state: Readonly<PhysicsState>;
    /** 
     * Snapshot of the most recent physics state update diff, which is updated at the end of every `tickEngine()` call.
     * `undefined` until initialization is complete.
     */
    readonly diff: Readonly<PhysicsDiff>;
    /**
     * Advances the simulation by one step and refreshes the `state` data.
     * @param syncOnly If true, only re-synchronizes the current state and doesn't advance time.
     */
    tickEngine(syncOnly: boolean): void;
    /**
     * Serializes the current state of the physics engine simulation into a JSON string.
     * @returns A string containing the simulation state in JSON format.
     */
    getPreset(): string;
    /**
     * Loads a preset string into the engine and refreshes state data.
     * @param jsonPreset A string containing the simulation state in JSON format.
     */
    loadPreset(jsonPreset: string): void;
    /**
     * Creates a new body with default properties.
     * @returns The id of the created body.
     */
    createBody(): BodyId;
    /**
     * Deletes an existing body from the simulation.
     * @param id The id of the body to delete.
     * @returns `true` if the body was deleted, or `false` if it wasn't found.
     */
    deleteBody(id: number): boolean;
    /**
     * Updates an existing body.
     * @param id The id of the body to update.
     * @param data Partial update data for the body.
     * @returns `true` if the body has been updated successfully, or `false` if it wasn't found.
     */
    updateBody(id: number, data: {
        enabled?: boolean | null,
        mass?: number | null,
        posX?: number | null,
        posY?: number | null,
        velX?: number | null,
        velY?: number | null,
    }): boolean;
    /**
     * Updates the current simulation.
     * @param data Partial update data for the simulation.
     */
    updateSimulation(data: {
        timeStep?: number,
        theta?: number,
        g_SI?: number,
        epsilon?: number
    }): void;
    /**
     * Gets a number of logged entries from the C# side of the bridge.
     * @param number The number of logs to get. -1 (or any other negative number) to get all available logs.
     * @returns An array of logged strings, from oldest to newest.
     */
    getLogs(number: number): string[];
    /** Clears the currently stored log entries. */
    clearLogs(): void;
}

/** API to interact with the .NET WASM simulation engine. */
class Bridge implements IBridge {
    static #instance: Bridge | null;

    static get instance(): Bridge {
        if(Bridge.#instance) return Bridge.#instance;
        else throw new Error("Cannot access Bridge instance before initialization or after exit.");
    }

    /** Initializes the Bridge and the underlying .NET runtime. Idempotent. */
    static async init(): Promise<Bridge> {
        if(Bridge.#instance) return Bridge.#instance;

        await DotNetHandler.init();
        const {engineBridge, runtime} = DotNetHandler;
        
        const stateManager = new StateManager({
            sim: {
                keys: engineBridge.GetSimStateLayout(),
                csTypes: engineBridge.GetSimStateCsTypes()
            },
            body: {
                keys: engineBridge.GetBodyStateLayout(),
                csTypes: engineBridge.GetBodyStateCsTypes(),
            }
        }, {
            getPointerData: engineBridge.GetPointerData,
            heapViewGetter: runtime.localHeapViewU8,
            log: __DEBUG__ ? console.log : undefined
        });

        Bridge.#instance = new Bridge(engineBridge, runtime, stateManager);
        return Bridge.#instance;
    }

    /** Exits the Bridge and the underlying .NET runtime. Idempotent. */
    static exit(): void {
        DotNetHandler.exit();
        Bridge.#instance = null;
    }

    static #nullUndefined<T>(val: T | null | undefined): T | null {
        if(val === undefined || val === null) return null;
        else return val;
    }

    #runtime: RuntimeAPI;
    #engineBridge: EngineBridgeAPI;
    #stateManager: StateManager;

    private constructor(
        engineBridge: EngineBridgeAPI,
        runtime: RuntimeAPI,
        stateManager: StateManager,
    ) {
        this.#engineBridge = engineBridge;
        this.#runtime = runtime;
        this.#stateManager = stateManager;
    }

    // Debug only getters
    get engineBridge() { return __DEBUG__ ? this.#engineBridge : undefined }
    get runtime() { return __DEBUG__ ? this.#runtime : undefined }
    get stateManager() { return __DEBUG__ ? this.#stateManager : undefined }

    //#region Interface Implementation

    get state() { return this.#stateManager.state; }

    get diff() { return this.#stateManager.diff; }

    tickEngine(syncOnly: boolean = false) {
        this.#engineBridge.Tick(syncOnly);
        this.#stateManager.refresh();
    }

    getPreset() { return this.#engineBridge.GetPreset(); }

    loadPreset(jsonPreset: string) {
        this.#engineBridge.LoadPreset(jsonPreset);
        this.#stateManager.refresh();
    }

    createBody() {
        return this.#engineBridge.CreateBody();
    }

    deleteBody(id: number) {
        return this.#engineBridge.DeleteBody(id);
    }

    updateBody(id: number, data: {
        enabled?: boolean | null,
        mass?: number | null,
        posX?: number | null,
        posY?: number | null,
        velX?: number | null,
        velY?: number | null,
    }) {
        const nullUndef = Bridge.#nullUndefined;
        return this.#engineBridge.UpdateBody(id,
            nullUndef(data.enabled),
            nullUndef(data.mass),
            nullUndef(data.posX),
            nullUndef(data.posY),
            nullUndef(data.velX),
            nullUndef(data.velY)
        );
    }
    
    updateSimulation(data: {
        timeStep?: number,
        theta?: number,
        g_SI?: number,
        epsilon?: number
    }) {
        const nullUndef = Bridge.#nullUndefined;
        return this.#engineBridge.UpdateSimulation(
            nullUndef(data.timeStep),
            nullUndef(data.theta),
            nullUndef(data.g_SI),
            nullUndef(data.epsilon)
        );
    }

    getLogs(number: number = -1) {
        if(!Number.isSafeInteger(number) || number === 0) return [];
        return this.#engineBridge.GetLogs(number);
    }

    clearLogs() {
        this.#engineBridge.ClearLogs();
    }

    //#endregion
}


export { type PhysicsStateBody, type PhysicsStateSim, type PhysicsState, type PhysicsDiff, type BodyId, IBridge, Bridge as default };