import { BodyId } from "@bridge";
import { ColorSource } from "pixi.js";

/** Represents the front-end exclusive data of a single body. */
export type BodyData = {
    readonly id: BodyId,
    name: string,
    tint: ColorSource,
}

export type SimData = {
    bgColor: ColorSource,
    enableOrbitPaths: boolean,
    enableVelocityTrais: boolean,
    enableBodyLabels: boolean,
}

/** Represents all front-end exclusive data at a given tick. */
export type AppData = {
    sim: SimData,
    bodies: Map<BodyId, BodyData>
}

/** Contains information AppData changes during the last engine tick. */
export type AppDataDiff = {
    /** The keys of SimData that were changed. */
    sim: Set<keyof SimData>,
    bodies: {
        /** The ids of newly created BodyData objects. */
        created: Set<BodyId>,
        /** The ids of BodyData objects that were updated. */
        deleted: Set<BodyId>,
        /** The ids of deleted BodyData objects. */
        updated: Set<BodyId> 
    }
}

export default class AppDataStore {
    static readonly DEFAULT_BODY_DATA_OMIT_ID_NAME: Readonly<Omit<BodyData, "id" | "name">> = {
        tint: "white"
    }
    
    static readonly DEFAULT_SIM_DATA: Readonly<SimData> = {
        bgColor: "black",
        enableOrbitPaths: true,
        enableBodyLabels: true,
        enableVelocityTrais: true
    } 

    static #appData = {
        sim: <SimData> { ...AppDataStore.DEFAULT_SIM_DATA },
        bodies: <Map<BodyId, BodyData>> new Map()
    }

    static #diff: AppDataDiff = { 
        sim: new Set(),
        bodies: { 
            created: new Set(),
            deleted: new Set(),
            updated: new Set(),
        }
    };

    static get diff() {
        return this.#diff as {
            readonly sim: ReadonlySet<keyof SimData>,
            readonly bodies: { updated: ReadonlySet<BodyId> }
        }
    }

    static get appData() {
        return this.#appData as {
            readonly sim: Readonly<SimData>,
            readonly bodies: ReadonlyMap<BodyId, Readonly<BodyData>>
        }
    }

    /**
     * Creates the BodyData entry for a given body's id (or overrides an existing one) with default data.
     * @param id ID of the respective body.
     */
    static createBodyData(id: BodyId): void {
        this.#appData.bodies.set(id, {
            ...AppDataStore.DEFAULT_BODY_DATA_OMIT_ID_NAME, 
            id, 
            name: `New Body #${id}`
        });
        this.#diff.bodies.created.add(id);
    }

    /**
     * Deletes a BodyData entry for a given body's id (or does nothing if none is found).
     * @param id ID of the respective body.
     */
    static deleteBodyData(id: BodyId): void {
        this.#appData.bodies.delete(id);
        this.#diff.bodies.deleted.add(id);
    }

    /**
     * Updates a BodyData entry with partial data.
     * @param id ID of the BodyData to delete.
     * @param updates Partial update data.
     * @returns `false` if no BodyData with the given id was found, `true` otherwise.
     */
    static updateBodyData(id: BodyId, updates: Partial<BodyData>): boolean {
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
    static updateSimulationData(updates: Partial<SimData>): void {
        for(const key of Object.keys(updates) as (keyof SimData)[]) {
            const newValue = updates[key];
            const oldValue = this.#appData.sim[key];

            if (newValue !== undefined && oldValue !== newValue) {
                this.#appData.sim[key] = newValue as any;
                this.#diff.sim.add(key);
            }
        }
    }
}