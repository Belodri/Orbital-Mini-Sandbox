import { BodyId, DiffData } from "@bridge";
import { ColorSource } from "pixi.js";

// TODO: Add preset verification and cleaning

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
    sim: AppStateSim,
    bodies: Map<BodyId, AppStateBody>
}

/** Contains information AppData changes during the last engine tick. */
export type AppDiff = {
    /** The keys of SimData that were changed. */
    sim: Set<keyof AppStateSim>,
    bodies: {
        /** The ids of newly created BodyData objects. */
        created: Set<BodyId>,
        /** The ids of BodyData objects that were updated. */
        deleted: Set<BodyId>,
        /** The ids of deleted BodyData objects. */
        updated: Set<BodyId> 
    }
}

type AppDataPreset = { sim: AppStateSim, bodies: AppStateBody[] }

/** Manages application-level, non-physics related data. */
export default class AppData {
    static readonly DEFAULT_BODY_DATA_OMIT_ID_NAME: Readonly<Omit<AppStateBody, "id" | "name">> = {
        tint: "white"
    }
    
    static readonly DEFAULT_SIM_DATA: Readonly<AppStateSim> = {
        bgColor: "black",
        enableOrbitPaths: true,
        enableBodyLabels: true,
        enableVelocityTrais: true
    } 

    #appData = {
        sim: <AppStateSim> { ...AppData.DEFAULT_SIM_DATA },
        bodies: <Map<BodyId, AppStateBody>> new Map()
    }

    #diff: AppDiff = { 
        sim: new Set(),
        bodies: { 
            created: new Set(),
            deleted: new Set(),
            updated: new Set(),
        }
    };

    get diff() {
        return this.#diff as {
            readonly sim: ReadonlySet<keyof AppStateSim>,
            readonly bodies: { updated: ReadonlySet<BodyId> }
        }
    }

    get appData() {
        return this.#appData as {
            readonly sim: Readonly<AppStateSim>,
            readonly bodies: ReadonlyMap<BodyId, Readonly<AppStateBody>>
        }
    }

    /**
     * Creates the BodyData entry for a given body's id (or overrides an existing one) with default data.
     * @param id ID of the respective body.
     * @param data The full state data of the body.
     */
    #createBodyData(id: BodyId, data?: AppStateBody): void {
        if(data && data.id !== id) throw new Error(`AppData: ID in 'data' argument (${data.id}) did not match body id (${id}).`);
        const state = data ?? {
            ...AppData.DEFAULT_BODY_DATA_OMIT_ID_NAME, 
            id, 
            name: `New Body #${id}`
        };

        this.#appData.bodies.set(id, state);
    }

    /**
     * Deletes a BodyData entry for a given body's id (or does nothing if none is found).
     * @param id ID of the respective body.
     */
    #deleteBodyData(id: BodyId): void {
        this.#appData.bodies.delete(id);
    }

    /**
     * Updates a BodyData entry with partial data.
     * @param id ID of the BodyData to delete.
     * @param updates Partial update data.
     * @returns `false` if no BodyData with the given id was found, `true` otherwise.
     */
    updateBodyData(id: BodyId, updates: Partial<AppStateBody>): boolean {
        const body = this.#appData.bodies.get(id);
        if(!body) return false;

        Object.assign(body, updates);
        this.#diff.bodies.updated.add(id);

        return true;
    }

    /**
     * Updates the front-end exlusive SimData with partial updates.
     * @param updates Partial update data.
     */
    updateSimulationData(updates: Partial<AppStateSim>): void {
        for(const key of Object.keys(updates) as (keyof AppStateSim)[]) {
            const newValue = updates[key];
            const oldValue = this.#appData.sim[key];

            if (newValue !== undefined && oldValue !== newValue) {
                this.#appData.sim[key] = newValue as any;
                this.#diff.sim.add(key);
            }
        }
    }

    /**
     * Creates and deletes BodyData objects in sycn with the provided diff data.
     * @param created The set of ids of created bodies
     * @param deleted The set of ids of deleted bodies
     */
    syncBodies(created: DiffData["bodies"]["created"], deleted: DiffData["bodies"]["deleted"]) {
        for(const id of created) this.#createBodyData(id);
        for(const id of deleted) this.#deleteBodyData(id);
    }

    /** 
     * Returns the current state of the AppData store as a preset that can be loaded via `loadPresetData()`.
     */
    getPresetData(): AppDataPreset {
        return {
            sim: this.#appData.sim,
            bodies: [...this.#appData.bodies.values()]
        };
    }

    /**
     * Loads preset data into the AppData store. Current data is overwritten.
     * @param preset The prset data to load.
     */
    loadPresetData(preset: AppDataPreset): void {
        this.updateSimulationData(preset.sim);

        this.#diff.bodies.updated.clear();
        this.#diff.bodies.deleted = new Set([...this.#appData.bodies.keys()]);

        for(const data of preset.bodies) this.#createBodyData(data.id, data);
    }
}

