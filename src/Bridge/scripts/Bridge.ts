import type { RuntimeAPI } from "@dotnet.d.ts";
import { DotNetHandler, type EngineBridgeAPI } from './DotNetHandler.ts';
import { StateManager, type PhysicsDiff, type PhysicsState, type PhysicsStateSim, type PhysicsStateBody, type BodyId  } from './StateManager.ts';

declare global {
    var Bridge: Bridge | undefined  // TODO: Remove
}

/**
 * Provides a static API to interact with the .NET WebAssembly simulation engine.
 */
class Bridge {
    static #DEBUG: boolean;
    static #runtime: RuntimeAPI;
    static #engineBridge: EngineBridgeAPI;
    static #stateManager: StateManager;

    /**
     * Initializes the Bridge and the underlying .NET runtime. Must be called once before any other methods.
     * @param debugMode Whether to run the Bridge and its components in debug mode. Cannot be changed during runtime.
     */
    static async initialize(debugMode: boolean = false) {
        Bridge.#DEBUG ??= debugMode;

        if(!Bridge.#engineBridge || !Bridge.#runtime) {
            const {engineBridge, runtime} = await DotNetHandler.init(Bridge.#DEBUG ? "DEVELOPMENT" : "PRODUCTION");
            Bridge.#runtime = runtime;
            Bridge.#engineBridge = engineBridge;
        }

        if(!Bridge.#stateManager) {
            Bridge.#stateManager = new StateManager({
                sim: {
                    keys: Bridge.#engineBridge.GetSimStateLayout(),
                    csTypes: Bridge.#engineBridge.GetSimStateCsTypes()
                },
                body: {
                    keys: Bridge.#engineBridge.GetBodyStateLayout(),
                    csTypes: Bridge.#engineBridge.GetBodyStateCsTypes(),
                }
            }, {
                getPointerData: Bridge.#engineBridge.GetPointerData,
                heapViewGetter: Bridge.#runtime.localHeapViewU8,
                log: Bridge.#DEBUG ? console.log : undefined
            });
        }

        if(Bridge.#DEBUG) { globalThis.Bridge = Bridge; }
    }

    // Debug only getters
    static get engineBridge() { return Bridge.#DEBUG ? Bridge.#engineBridge : undefined }
    static get runtime() { return Bridge.#DEBUG ? Bridge.#runtime : undefined }
    static get stateManager() { return Bridge.#DEBUG ? Bridge.#stateManager : undefined }

    /** 
     * Snapshot of the most recent physics state, which is updated at the end of every `tickEngine()` call.
     * `undefined` until initialization is complete.
     */
    static get state() { return Bridge.#stateManager.state; }

    /** 
     * Snapshot of the most recent physics state update diff, which is updated at the end of every `tickEngine()` call.
     * `undefined` until initialization is complete.
     */
    static get diff() { return Bridge.#stateManager.diff; }

    /**
     * Advances the simulation by one step and refreshes the `state` data.
     * @param syncOnly If true, only re-synchronizes the current state and doesn't advance time.
     */
    static tickEngine(syncOnly: boolean = false) {
        Bridge.#engineBridge.Tick(syncOnly);
        Bridge.#stateManager.refresh();
    }

    /**
     * Serializes the current state of the physics engine simulation into a JSON string.
     * @returns A string containing the simulation state in JSON format.
     */
    static getPreset() { return Bridge.#engineBridge.GetPreset(); }

    /**
     * Loads a preset string into the engine and refreshes state data.
     * @param jsonPreset A string containing the simulation state in JSON format.
     */
    static loadPreset(jsonPreset: string) {
        Bridge.#engineBridge.LoadPreset(jsonPreset);
        Bridge.#stateManager.refresh();
    }

    /**
     * Creates a new body with default properties.
     * @returns The id of the created body.
     */
    static createBody() {
        return Bridge.#engineBridge.CreateBody();
    }

    /**
     * Deletes an existing body from the simulation.
     * @param id The id of the body to delete.
     * @returns `true` if the body was deleted, or `false` if it wasn't found.
     */
    static deleteBody(id: number) {
        return Bridge.#engineBridge.DeleteBody(id);
    }


    static #nullUndefined<T>(val: T | null | undefined): T | null {
        if(val === undefined || val === null) return null;
        else return val;
    }

    /**
     * Updates an existing body.
     * @param id The id of the body to update.
     * @param data Partial update data for the body.
     * @returns `true` if the body has been updated successfully, or `false` if it wasn't found.
     */
    static updateBody(id: number, data: {
        enabled?: boolean | null,
        mass?: number | null,
        posX?: number | null,
        posY?: number | null,
        velX?: number | null,
        velY?: number | null,
    }) {
        return Bridge.#engineBridge.UpdateBody(id,
            Bridge.#nullUndefined(data.enabled),
            Bridge.#nullUndefined(data.mass),
            Bridge.#nullUndefined(data.posX),
            Bridge.#nullUndefined(data.posY),
            Bridge.#nullUndefined(data.velX),
            Bridge.#nullUndefined(data.velY)
        );
    }
    
    /**
     * Updates the current simulation.
     * @param data Partial update data for the simulation.
     */
    static async updateSimulation(data: {
        timeStep?: number,
        theta?: number,
        g_SI?: number,
        epsilon?: number
    }) {
        return Bridge.#engineBridge.UpdateSimulation(
            Bridge.#nullUndefined(data.timeStep),
            Bridge.#nullUndefined(data.theta),
            Bridge.#nullUndefined(data.g_SI),
            Bridge.#nullUndefined(data.epsilon)
        );
    }

    /**
     * Gets a number of logged entries from the C# side of the bridge.
     * @param number The number of logs to get. -1 (or any other negative number) to get all available logs.
     * @returns An array of logged strings, from oldest to newest.
     */
    static getLogs(number: number = -1) {
        if(!Number.isSafeInteger(number) || number === 0) return;
        return Bridge.#engineBridge.GetLogs(number);
    }

    /**
     * Clears the currently stored log entries.
     */
    static clearLogs() {
        Bridge.#engineBridge.ClearLogs();
    }
}


export { type PhysicsStateBody, type PhysicsStateSim, type PhysicsState, type PhysicsDiff, type BodyId, Bridge as default };