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
    /** The ids of bodies that were neither created nor destroyed. */
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
     * @returns A promise that resolves when initialization is complete.
     * @throws {Error} if the Bridge has already been initialized.
     */
    static initialize(): Promise<void>;

    /**
     * Advances the simulation by one step and refreshes the `simState` data.
     * @param timestamp The high-resolution timestamp, typically from `requestAnimationFrame()`.
     * @returns An object detailing which body IDs were created, updated, or deleted during the tick.
     * @throws {Error} if the simulation engine reports an error.
     */
    static tickEngine(timestamp: number): BodyDiffData;

    /**
     * A test method to create a simulation with a specified number of bodies.
     * @param bodyCount The number of bodies to create in the test simulation.
     */
    static _createTestSim(bodyCount: number): void;
}