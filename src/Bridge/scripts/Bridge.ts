import type { RuntimeAPI } from "@dotnet.d.ts";
import type { BodyStateLayout, SimStateLayout } from "@LayoutRecords.d.ts";
import { DotNetHandler, type EngineBridgeAPI } from './DotNetHandler.ts';
import { StateManager } from './StateManager.ts';

// TODO: Remove TimeoutLoopHandler once the WebApp fully controlling the Bridge has been implemented


// Redeclare for semantics and to prevent extension.

/** Represents the state of a single body in the simulation. */
type PhysicsStateBody = { [Property in keyof BodyStateLayout] : BodyStateLayout[Property] }
type PhysicsStateSim = { [Property in keyof SimStateLayout] : SimStateLayout[Property] };
type BodyId = PhysicsStateBody["id"];

/**
 * Represents the entire state of the simulation at a given tick.
 * Contains a map of all bodies and other global simulation properties.
 */
type PhysicsState = {
    sim: PhysicsStateSim,
    bodies: Map<BodyId, PhysicsStateBody>
}

/**
 * Contains information physics engine state changes* during the last engine tick.
 */
type PhysicsDiff = {
    /** The keys of SimState that were changed. */
    sim: Set<keyof PhysicsStateSim>,
    bodies: {
        /** The ids of newly created bodies. */
        created: Set<BodyId>,
        /** The ids of bodies that were updated. */
        deleted: Set<BodyId>,
        /** The ids of deleted bodies. */
        updated: Set<BodyId>
    }
}

declare global {
    var Bridge: Bridge | undefined
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
        this.#DEBUG ??= debugMode;

        if(!this.#engineBridge || !this.#runtime) {
            const {engineBridge, runtime} = await DotNetHandler.init(this.#DEBUG ? "DEVELOPMENT" : "PRODUCTION");
            this.#runtime = runtime;
            this.#engineBridge = engineBridge;
        }

        if(!this.#stateManager) {
            this.#stateManager = new StateManager({
                sim: this.#engineBridge.GetSimStateLayout(),
                body: this.#engineBridge.GetBodyStateLayout()
            }, {
                getPointerData: this.#engineBridge.GetPointerData,
                heapViewGetter: this.#runtime.localHeapViewU8,
                log: this.#DEBUG ? console.log : undefined
            });
        }

        if(this.#DEBUG) { globalThis.Bridge = this; }
    }

    // Debug only getters
    static get engineBridge() { return this.#DEBUG ? this.#engineBridge : undefined }
    static get runtime() { return this.#DEBUG ? this.#runtime : undefined }
    static get stateManager() { return this.#DEBUG ? this.#stateManager : undefined }

    /** 
     * Snapshot of the most recent physics state, which is updated at the end of every `tickEngine()` call.
     * `undefined` until initialization is complete.
     */
    static get state() { return this.#stateManager.state; }

    /** 
     * Snapshot of the most recent physics state update diff, which is updated at the end of every `tickEngine()` call.
     * `undefined` until initialization is complete.
     */
    static get diff() { return this.#stateManager.diff; }

    /**
     * Advances the simulation by one step and refreshes the `state` data.
     * @param syncOnly If true, only re-synchronizes the current state and doesn't advance time.
     */
    static tickEngine(syncOnly: boolean = false) {
        this.#engineBridge.Tick(syncOnly);
        this.#stateManager.refresh();
    }

    /**
     * Serializes the current state of the physics engine simulation into a JSON string.
     * @returns A string containing the simulation state in JSON format.
     */
    static getPreset() { return this.#engineBridge.GetPreset(); }

    /**
     * Loads a preset string into the engine and refreshes state data.
     * @param jsonPreset A string containing the simulation state in JSON format.
     */
    static loadPreset(jsonPreset: string) {
        this.#engineBridge.LoadPreset(jsonPreset);
        this.#stateManager.refresh();
    }

    /**
     * Creates a new body with default properties.
     * @returns The id of the created body.
     */
    static createBody() {
        return this.#engineBridge.CreateBody();
    }

    /**
     * Deletes an existing body from the simulation.
     * @param id The id of the body to delete.
     * @returns `true` if the body was deleted, or `false` if it wasn't found.
     */
    static deleteBody(id: number) {
        return this.#engineBridge.DeleteBody(id);
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
        return this.#engineBridge.UpdateBody(id,
            this.#nullUndefined(data.enabled),
            this.#nullUndefined(data.mass),
            this.#nullUndefined(data.posX),
            this.#nullUndefined(data.posY),
            this.#nullUndefined(data.velX),
            this.#nullUndefined(data.velY)
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
        return this.#engineBridge.UpdateSimulation(
            this.#nullUndefined(data.timeStep),
            this.#nullUndefined(data.theta),
            this.#nullUndefined(data.g_SI),
            this.#nullUndefined(data.epsilon)
        );
    }

    /**
     * Gets a number of logged entries from the C# side of the bridge.
     * @param number The number of logs to get. -1 (or any other negative number) to get all available logs.
     * @returns An array of logged strings, from oldest to newest.
     */
    static getLogs(number: number = -1) {
        if(!Number.isSafeInteger(number) || number === 0) return;
        return this.#engineBridge.GetLogs(number);
    }

    /**
     * Clears the currently stored log entries.
     */
    static clearLogs() {
        this.#engineBridge.ClearLogs();
    }
}


export { type PhysicsStateBody, type PhysicsStateSim, type PhysicsState, type PhysicsDiff, type BodyId, Bridge as default };