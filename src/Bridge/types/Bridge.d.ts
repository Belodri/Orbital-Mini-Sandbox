import type { SimStateLayout, BodyStateLayout } from "./LayoutRecords";

/**
 * Represents the state of a single body in the simulation.
 */
export interface BodyStateData extends BodyStateLayout { }

/**
 * Represents the entire state of the simulation at a given tick.
 * Contains a map of all bodies and other global simulation properties.
 */
export interface SimState extends SimStateLayout {
    /** A map of body IDs to their state data. */
    readonly bodies: Map<number, BodyStateData>;
}

/**
 * Contains information about which bodies were created, updated, or deleted
 * during the last engine tick.
 */
export interface BodyDiffData {
    /** The ids of newly created bodies. */
    created: Set<number>;
    /** The ids of bodies that were updated. */
    updated: Set<number>;
    /** The ids of deleted bodies. */
    deleted: Set<number>;
}

/**
 * Provides a static API to interact with the .NET WebAssembly simulation engine.
 */
export default class Bridge {
    /**
     * The current state of the simulation.
     * This object is updated after each call to `tickEngine()`.
     * It is `undefined` until the first tick after initialization is complete.
     */
    static readonly simState: SimState | undefined;

    /**
     * Initializes the Bridge and the underlying .NET runtime. Must be called once before any other methods.
     * @param {boolean} [debugMode=false]           If true enables debug logging and exposes the `Bridge` to the `globalThis`.
     * @param {boolean} [diagnosticsTracing=false]  If "true", writes diagnostic messages during runtime startup and execution to the browser console.
     * @returns A promise that resolves when initialization is complete.
     * @throws {Error} if the Bridge has already been initialized.
     */
    static initialize(debugMode: bool = false, diagnosticsTracing: bool = false): Promise<void>;

    /**
     * Sets the maximum interval in which promises are handled, 
     * in cases where `tickEngine` isn't called regularly.
     * @param ms The interval time in ms. Cannot be less than 50ms! Default 100ms.
     */
    static setMaxPromiseInterval(ms?: number) : void;

    /**
     * Advances the simulation by one step and refreshes the `simState` data.
     * @param timestamp The high-resolution timestamp, typically from `requestAnimationFrame()`.
     * @returns An object detailing which body IDs were created, updated, or deleted during the tick.
     */
    static tickEngine(timestamp: number): BodyDiffData;

    /**
     * Serializes the current state of the physics engine simulation into a JSON string.
     * @returns A string containing the simulation state in JSON format.
     */
    static getPreset(): string;

    /**
     * Loads a preset string into the engine and refreshes simState data.
     * @param jsonPreset A string containing the simulation state in JSON format.
     * @returns An object detailing which body IDs were created, updated, or deleted during the tick.
     */
    static loadPreset(jsonPreset: string): BodyDiffData;

    /**
     * Creates a new (default disabled) body in the simulation.
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
     * @param id        The unique id for the body to update.
     * @param values    The new values for the body.
     * @returns Promise that resolves to `true` if the body has been updated successfully, or `false` if it wasn't found.
     */
    static updateBody(id: number, values: Partial<{ 
        enabled: boolean|number,
        mass: number,
        posX: number,
        posY: number,
        velX: number,
        velY: number,
        accX: number,
        accY: number
    }>): Promise<boolean>;

    /**
     * Updates the current simulation.
     * @param values The new values for the simulation parameters.
     * @returns A promise that resolves when the simulation parameters have been updated successfully.
     */
    static updateSimulation(values: Partial<{
        timeStep: number,
        theta: number,
        g_SI: number,
        epsilon: number
    }>): Promise<void>;

    /**
     * Registers a function to be called every time the simState has been refreshed.
     * Only one such function can be registered.
     * @param fn        Receives a `BodyDiffData` object as an argument.
     * @param thisArg   An object to which the this keyword refers inside the callback function.
     */
    static registerOnTickCallback(fn: Function, thisArg: any): void;
}