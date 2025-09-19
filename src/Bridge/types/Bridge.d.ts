import type { RuntimeAPI } from '../types/dotnet';
import type { BodyStateLayout, SimStateLayout } from '../types/LayoutRecords';
import { type EngineBridgeAPI } from './DotNetHandler.ts';
import { StateManager } from './StateManager.ts';
/** Represents the state of a single body in the simulation. */
export type BodyState = {
    [Property in keyof BodyStateLayout]: BodyStateLayout[Property];
};
export type SimState = {
    [Property in keyof SimStateLayout]: SimStateLayout[Property];
};
/**
 * Represents the entire state of the simulation at a given tick.
 * Contains a map of all bodies and other global simulation properties.
 */
export type StateData = {
    sim: SimState;
    bodies: Map<BodyState["id"], BodyState>;
};
/**
 * Contains information physics engine state changes* during the last engine tick.
 */
export type DiffData = {
    /** The keys of SimState that were changed. */
    sim: Set<keyof SimState>;
    bodies: {
        /** The ids of newly created bodies. */
        created: Set<BodyState["id"]>;
        /** The ids of bodies that were updated. */
        deleted: Set<BodyState["id"]>;
        /** The ids of deleted bodies. */
        updated: Set<BodyState["id"]>;
    };
};
declare global {
    var Bridge: Bridge | undefined;
}
/**
 * Provides a static API to interact with the .NET WebAssembly simulation engine.
 */
export declare class Bridge {
    #private;
    /**
     * Initializes the Bridge and the underlying .NET runtime. Must be called once before any other methods.
     * @param onStateChangeCallback Callback to notify the consumer about a changed state & diff.
     * @param debugMode Whether to run the Bridge and its components in debug mode. Cannot be changed during runtime.
     */
    static initialize(onStateChangeCallback: () => void, debugMode?: boolean): Promise<void>;
    static get engineBridge(): EngineBridgeAPI | undefined;
    static get runtime(): RuntimeAPI | undefined;
    static get stateManager(): StateManager | undefined;
    static get timeoutLoop(): TimeoutLoopHandler | undefined;
    /**
     * Snapshot of the most recent physics state, which is updated at the end of every `tickEngine()` call.
     * `undefined` until initialization is complete.
     */
    static get state(): Readonly<import("./StateManager.ts").StateData>;
    /**
     * Snapshot of the most recent physics state update diff, which is updated at the end of every `tickEngine()` call.
     * `undefined` until initialization is complete.
     */
    static get diff(): Readonly<import("./StateManager.ts").DiffData>;
    /**
     * Advances the simulation by one step and refreshes the `simState` data.
     */
    static tickEngine(): void;
    /**
     * Serializes the current state of the physics engine simulation into a JSON string.
     * @returns A string containing the simulation state in JSON format.
     */
    static getPreset(): string;
    /**
     * Loads a preset string into the engine and refreshes simState data.
     * @param jsonPreset A string containing the simulation state in JSON format.
     */
    static loadPreset(jsonPreset: string): void;
    /**
     * Creates a new body with default properties.
     * @returns Promise that resolves to the id of the created body
     */
    static createBody(): Promise<number>;
    /**
     * Deletes an existing body from the simulation.
     * @param id The id of the body to delete.
     * @returns Promise that resolves to `true` if the body was deleted, or `false` if it wasn't found.
     */
    static deleteBody(id: number): Promise<boolean>;
    /**
     * Updates an existing body.
     * @param id The id of the body to update.
     * @param data Partial update data for the body.
     * @returns Promise that resolves to `true` if the body has been updated successfully, or `false` if it wasn't found.
     */
    static updateBody(id: number, data: {
        enabled?: number | boolean | null;
        mass?: number | null;
        posX?: number | null;
        posY?: number | null;
        velX?: number | null;
        velY?: number | null;
    }): Promise<boolean>;
    /**
     * Updates the current simulation.
     * @param data Partial update data for the simulation.
     * @returns Promise that resolves when the simulation has been updated.
     */
    static updateSimulation(data: {
        timeStep?: number;
        theta?: number;
        g_SI?: number;
        epsilon?: number;
    }): Promise<void>;
    /**
     * Gets a number of logged entries from the C# side of the bridge.
     * @param number The number of logs to get. -1 (or any other negative number) to get all available logs.
     * @returns An array of logged strings, from oldest to newest.
     */
    static getLogs(number?: number): string[] | undefined;
    /**
     * Clears the currently stored log entries.
     */
    static clearLogs(): void;
}
declare class TimeoutLoopHandler {
    #private;
    constructor(timeoutIntervalInMs: number, callbackFn: () => void);
    start(): void;
    cancel(): void;
}
export {};
//# sourceMappingURL=Bridge.d.ts.map