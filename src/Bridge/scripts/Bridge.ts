import type { RuntimeAPI } from "@dotnet.d.ts";
import type { BodyStateLayout, SimStateLayout } from "@LayoutRecords.d.ts";
import { DotNetHandler, type EngineBridgeAPI } from './DotNetHandler.ts';
import { StateManager } from './StateManager.ts';

// TODO: Remove TimeoutLoopHandler once the WebApp fully controlling the Bridge has been implemented


// Redeclare for semantics and to prevent extension.

/** Represents the state of a single body in the simulation. */
type BodyState = { [Property in keyof BodyStateLayout] : BodyStateLayout[Property] }
type SimState = { [Property in keyof SimStateLayout] : SimStateLayout[Property] };


/**
 * Represents the entire state of the simulation at a given tick.
 * Contains a map of all bodies and other global simulation properties.
 */
type StateData = {
    sim: SimState,
    bodies: Map<BodyState["id"], BodyState>
}

/**
 * Contains information physics engine state changes* during the last engine tick.
 */
type DiffData = {
    /** The keys of SimState that were changed. */
    sim: Set<keyof SimState>,
    bodies: {
        /** The ids of newly created bodies. */
        created: Set<BodyState["id"]>,
        /** The ids of bodies that were updated. */
        deleted: Set<BodyState["id"]>,
        /** The ids of deleted bodies. */
        updated: Set<BodyState["id"]>
    }
}

declare global {
    var Bridge: Bridge | undefined
}

/**
 * Provides a static API to interact with the .NET WebAssembly simulation engine.
 */
class Bridge {
    static #CONFIG = {
        pausedPromiseIntervalInMs: 100,
    }

    static #DEBUG: boolean;

    static #runtime: RuntimeAPI;
    static #engineBridge: EngineBridgeAPI;
    static #stateManager: StateManager;
    static #timeoutLoop: TimeoutLoopHandler;

    static #onStateChangeCallback: () => void;

    /**
     * Initializes the Bridge and the underlying .NET runtime. Must be called once before any other methods.
     * @param onStateChangeCallback Callback to notify the consumer about a changed state & diff.
     * @param debugMode Whether to run the Bridge and its components in debug mode. Cannot be changed during runtime.
     */
    static async initialize(onStateChangeCallback: () => void, debugMode: boolean = false) {
        this.#DEBUG ??= debugMode;
        this.#onStateChangeCallback = onStateChangeCallback;

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

        if(!this.#timeoutLoop) {
            this.#timeoutLoop = new TimeoutLoopHandler(this.#CONFIG.pausedPromiseIntervalInMs, () => this.tickEngine(true));
        }

        if(this.#DEBUG) { globalThis.Bridge = this; }
    }

    // Debug only getters
    static get engineBridge() { return this.#DEBUG ? this.#engineBridge : undefined }
    static get runtime() { return this.#DEBUG ? this.#runtime : undefined }
    static get stateManager() { return this.#DEBUG ? this.#stateManager : undefined }
    static get timeoutLoop() { return this.#DEBUG ? this.#timeoutLoop : undefined }


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
     * @param [syncOnly=false] If true, only re-synchronizes the current state and doesn't advance time.
     */
    static tickEngine(syncOnly: boolean = false) {
        this.#timeoutLoop.cancel();
        this.#engineBridge.Tick(syncOnly);
        this.#stateManager.refresh();
        this.#onStateChangeCallback();
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
     * 
     * Promise resolves after state has been written into shared memory but before the Bridge.state has been updated to reflect it! 
     * 
     * @returns Promise that resolves to the id of the created body.
     */
    static async createBody() {
        this.#timeoutLoop.start();
        return this.#engineBridge.CreateBody();
    }

    /**
     * Deletes an existing body from the simulation.
     * 
     * Promise resolves after state has been written into shared memory but before the Bridge.state has been updated to reflect it! 
     * 
     * @param id The id of the body to delete.
     * @returns Promise that resolves to `true` if the body was deleted, or `false` if it wasn't found.
     */
    static async deleteBody(id: number) {
        this.#timeoutLoop.start();
        return this.#engineBridge.DeleteBody(id);
    }


    static #numNull(val: any, allowBoolIn = false) {
        if (typeof val === 'number') return val;
        if (allowBoolIn && typeof val === 'boolean') return val ? 1 : 0;
        return null;
    }

    /**
     * Updates an existing body.
     * 
     * Promise resolves after state has been written into shared memory but before the Bridge.state has been updated to reflect it! 
     * 
     * @param id The id of the body to update.
     * @param data Partial update data for the body.
     * @returns Promise that resolves to `true` if the body has been updated successfully, or `false` if it wasn't found.
     */
    static async updateBody(id: number, data: {
        enabled?: number | boolean | null,
        mass?: number | null,
        posX?: number | null,
        posY?: number | null,
        velX?: number | null,
        velY?: number | null,
    }) {
        this.#timeoutLoop.start();
        return this.#engineBridge.UpdateBody(id,
            !!this.#numNull(data.enabled, true),  // Cast to bool if number; remove once the TODO in LayoutRecords.cs is completed
            this.#numNull(data.mass),
            this.#numNull(data.posX),
            this.#numNull(data.posY),
            this.#numNull(data.velX),
            this.#numNull(data.velY)
        );
    }
    
    /**
     * Updates the current simulation.
     * 
     * Promise resolves after state has been written into shared memory but before the Bridge.state has been updated to reflect it! 
     * 
     * @param data Partial update data for the simulation.
     * @returns Promise that resolves when the simulation has been updated.
     */
    static async updateSimulation(data: {
        timeStep?: number,
        theta?: number,
        g_SI?: number,
        epsilon?: number
    }) {
        this.#timeoutLoop.start();
        return this.#engineBridge.UpdateSimulation(
            this.#numNull(data.timeStep),
            this.#numNull(data.theta),
            this.#numNull(data.g_SI),
            this.#numNull(data.epsilon)
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

class TimeoutLoopHandler {
    #timeoutIntervalInMs: number;
    #callback: () => void;
    #timeoutId: number | null = null;

    constructor(timeoutIntervalInMs: number, callbackFn: () => void) {
        const validTimeout = Number.isSafeInteger(timeoutIntervalInMs)
            && timeoutIntervalInMs > 0;
        if(!validTimeout) throw new Error(`Invalid argument: 'timeoutIntervalInMs' must be a positive integer but was ${timeoutIntervalInMs}`);
        this.#timeoutIntervalInMs = timeoutIntervalInMs;
        this.#callback = callbackFn;
    }

    start() {
        if(this.#timeoutId) return;

        this.#timeoutId = window.setTimeout(() => {
            try {
                this.#callback()
            } finally {
                this.#timeoutId = null;
            }
        }, this.#timeoutIntervalInMs);
    }

    cancel() {
        if(!this.#timeoutId) return;
        clearTimeout(this.#timeoutId);
        this.#timeoutId = null;
    }
}

export { type BodyState, type SimState, type StateData, type DiffData, Bridge as default };