/**
 * @typedef {object} BodyMetaData
 * @property {string} name          The display name of the celestial body.
 * @property {string|number} tint   The color tint applied to the body's sprite (e.g., 'white', 0xff0000).
 */

/** Manages application-level, non-physics related metadata for celestial bodies. */
export default class AppDataManager {
    /** @type {BodyMetaData} */
    static DEFAULT_BODY_DATA = {
        name: "New Body",
        tint: "white",
    }

    /** @type {Map<number, BodyMetaData>} */
    #bodyData = new Map();

    /** @returns {Readonly<Map<number, BodyMetaData>>} */
    get bodyData() { return this.#bodyData; }

    /** @type {Map<number, Partial<BodyMetaData>>} */
    #queuedBodyUpdates = new Map();

    /** @returns {Readonly<Map<number, Partial<BodyMetaData>>>} */
    get queuedBodyUpdates() { return this.#queuedBodyUpdates; }

    /** 
     * Cached Set to reduce GC pressure in `handleQueuedUpdates`.
     * @type {Set<number>}
     */
    #lastUpdatedCache = new Set();

    /**
     * Queues updates for a given body's metadata to be executed on the next frame.
     * If an update for a body is already queued, the new update data will merge with
     * and override the existing queued data.
     * @param {number} id 
     * @param {Partial<BodyMetaData>} updates 
     * @returns {boolean}
     */
    queueBodyDataUpdate(id, updates={}) {
        if(!this.#bodyData.has(id)) return false;

        const obj = this.#queuedBodyUpdates.get(id) ?? {};
        this.#queuedBodyUpdates.set(id, {
            ...obj,
            ...updates
        });

        return true;
    }

    /** 
     * Initializes a newly created body with default metadata.
     * Called by the orchestrator in response to the simulation's `create` event.
     * @param {number} id
     */
    onCreateBody(id) {
        if(this.bodyData.has(id)) return;
        this.bodyData.set(id, { ...AppDataManager.DEFAULT_BODY_DATA });
    }

    /** 
     * Handles the deletion of a body's metadata.
     * Called by the orchestrator in response to the simulation's `delete` event.
     * @param {number} id
     */
    onDeleteBody(id) {
        if(!this.bodyData.has(id)) return;
        this.bodyData.delete(id);
    }

    /**
     * Handles the execution of queued body metadata updates and returns a Set
     * containing the IDs of all bodies that were modified.
     * @returns {Set<number>} 
     */
    handleQueuedUpdates() {
        this.#lastUpdatedCache.clear();

        for(const [id, updateData] of this.#queuedBodyUpdates.entries()) {
            const body = this.bodyData.get(id);
            if(!body) continue;

            for(const [k, v] of Object.entries(updateData)) {
                if(k in body) body[k] = v;
            }

            this.#lastUpdatedCache.add(id);
            this.#queuedBodyUpdates.delete(id);
        }

        return this.#lastUpdatedCache;
    }

    //#region Preset

    /**
     * Returns an array of key-value pairs of the manager's bodyData store.
     * @returns {[number, BodyMetaData][]}
     */
    getPresetData() {
        return [...this.bodyData.entries()];
    }

    /**
     * Loads the parsed bodyData into the manager's bodyData store.
     * @param {[number, BodyMetaData][]} bodyData 
     * @returns {void}
     */
    loadPresetData(bodyData) {
        this.#bodyData.clear();
        for(const [k, v] of bodyData) this.#bodyData.set(k, v);
    }

    //#endregion
}
