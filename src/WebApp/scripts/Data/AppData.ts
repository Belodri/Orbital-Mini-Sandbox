import { BodyId } from "@bridge";
import { ColorSource } from "pixi.js";

// TODO: Add preset verification and cleaning

const DEFAULT_SIM_DATA: Readonly<AppStateSim> = {
    bgColor: "black",
    enableOrbitPaths: true,
    enableBodyLabels: true,
    enableVelocityTrais: true
} as const;

const DEFAULT_BODY_DATA_OMIT_ID_NAME: Readonly<Omit<AppStateBody, "id" | "name">> = {
    tint: "white"
} as const;

/** Represents the front-end exclusive data of a single body. */
export type AppStateBody = {
    readonly id: BodyId,
    /** The display name of the celestial body. */
    name: string,
    /** The color tint applied to the body's sprite (e.g., 'white', 0xff0000). */
    tint: ColorSource,
}

export type AppStateSim = {
    bgColor: ColorSource,
    enableOrbitPaths: boolean,
    enableVelocityTrais: boolean,
    enableBodyLabels: boolean,
}

/** Represents all front-end exclusive data at a given tick. */
export type AppState = {
    readonly sim: Readonly<AppStateSim>,
    readonly bodies: ReadonlyMap<BodyId, Readonly<AppStateBody>>
}

/** Contains information AppData changes during the last engine tick. */
export type AppDiff = {
    /** The keys of SimData that were changed. */
    readonly sim: ReadonlySet<keyof AppStateSim>,
    /** 
     * The ids of BodyData objects that were updated.  
     * We only track updated bodies because the created and deleted bodies are synchronized with the PhysicsDiff.
     */
    readonly updatedBodies: ReadonlySet<BodyId> 
}

type AppDataPreset = { sim: AppStateSim, bodies: AppStateBody[] }

/** Manages application-level, non-physics related data. */
export interface IAppData {
    /** Contains information AppData changes during the last engine tick. */
    diff: AppDiff;
    /** Represents all front-end exclusive data at a given tick. */
    state: AppState;
    /**
     * Updates a BodyData entry with partial data.
     * @param id ID of the BodyData to update.
     * @param updates Partial update data.
     * @returns `false` if no BodyData with the given id was found, `true` otherwise.
     */
    updateBodyData(id: BodyId, updates: Partial<Omit<AppStateBody, "id">>): boolean;
    /**
     * Updates the front-end exlusive SimData with partial updates.
     * @param updates Partial update data.
     */
    updateSimulationData(updates: Partial<AppStateSim>): void 
    /**
     * Synchronizes diff data with the physics engine, creating and deleting BodyData objects as required.  
     * Swaps diff buffers, ensuring that any update calls after this don't pullute the current frame.
     * @param created The set of ids of created bodies in the physics engine.
     * @param deleted The set of ids of deleted bodies in the physics engine.
     */
    syncDiff(created: ReadonlySet<BodyId>, deleted: ReadonlySet<BodyId>): void;
    /** 
     * Returns the current state of the AppData store as a preset that can be loaded via `loadPresetData()`.
     */
    getPresetData(): AppDataPreset;
    /**
     * Loads preset data into the AppData store. Current data is overwritten.
     * @param preset The preset data to load.
     */
    loadPresetData(preset: AppDataPreset): void;
}

export default class AppData implements IAppData {
    //#region Interface Implementation

    get diff(): AppDiff { return this.#diff }
    get state(): AppState { return this.#state }

    updateBodyData(id: BodyId, updates: Partial<Omit<AppStateBody, "id">>): boolean {
        const body = this.#state.bodies.get(id);
        if(!body) return false;

        let hasChanged = false;
        for(const key of Object.keys(updates) as (keyof Omit<AppStateBody, "id">)[]) {
            const newValue = updates[key];
            const oldValue = body[key];

            if(newValue !== undefined && oldValue !== newValue) {
                body[key] = newValue as any;
                hasChanged = true;
            }
        }

        if(hasChanged) this.#nextFrameDiff.updatedBodies.add(id);

        return true;
    }

    updateSimulationData(updates: Partial<AppStateSim>): void {
        for(const key of Object.keys(updates) as (keyof AppStateSim)[]) {
            const newValue = updates[key];
            const oldValue = this.#state.sim[key];

            if (newValue !== undefined && oldValue !== newValue) {
                this.#state.sim[key] = newValue as any;
                this.#nextFrameDiff.sim.add(key);
            }
        }
    }

    syncDiff(created: ReadonlySet<BodyId>, deleted: ReadonlySet<BodyId>) {
        // The PhysicsDiff ensures that an id can never be in both created and deleted sets at the same time.
        for(const id of created) this.#createBodyData(id);
        for(const id of deleted) this.#deleteBodyData(id);

        this.#diff.sim.clear();
        this.#diff.updatedBodies.clear();
        [this.#diff, this.#nextFrameDiff] = [this.#nextFrameDiff, this.#diff];
    }

    getPresetData(): AppDataPreset {
        return {
            sim: this.#state.sim,
            bodies: [...this.#state.bodies.values()]
        };
    }

    loadPresetData(preset: AppDataPreset): void {
        this.#state.bodies.clear();
        this.#diff.updatedBodies.clear();

        for(const data of preset.bodies) this.#createBodyData(data.id, data);

        this.updateSimulationData(preset.sim);
    }

    //#endregion

    //#region Private implementation

    #state = {
        sim: { ...DEFAULT_SIM_DATA } as AppStateSim,
        bodies: new Map() as Map<BodyId, AppStateBody>
    }

    #diff = { 
        sim: new Set() as Set<keyof AppStateSim>,
        updatedBodies: new Set as Set<BodyId>
    };

    #nextFrameDiff = {
        sim: new Set() as Set<keyof AppStateSim>,
        updatedBodies: new Set as Set<BodyId>
    };

    /**
     * Creates a new BodyData entry for a given body's id with default data if it doesn't exist already.
     * @param id ID of the respective body.
     * @param data The full state data of the body.
     */
    #createBodyData(id: BodyId, data?: AppStateBody): void {
        if(data && data.id !== id) throw new Error(`AppData: ID in 'data' argument (${data.id}) did not match body id (${id}).`);
        if(this.#state.bodies.has(id)) return;   

        const state = data ?? {
            ...DEFAULT_BODY_DATA_OMIT_ID_NAME, 
            id, 
            name: `New Body #${id}`
        };

        this.#state.bodies.set(id, state);
    }

    /**
     * Deletes a BodyData entry for a given body's id (or does nothing if none is found).
     * @param id ID of the respective body.
     */
    #deleteBodyData(id: BodyId): void {
        this.#state.bodies.delete(id)
    }

    //#endregion
}
