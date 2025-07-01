import { dotnet as _dotnet } from './_framework/dotnet.js';
/** @type {import('../types/dotnet.js').dotnet} */
const dotnet = _dotnet;

/** @import {BodyStateData, SimState, BodyDiffData} from "../types/Bridge.js" */

/**
 * @import { MonoConfig, RuntimeAPI } from '../types/dotnet.js'
 */

export default class Bridge {
    static #CONFIG = {
        DEBUG_MODE: true,
        NAMESPACE: "Bridge",
        CLASS_NAME: "EngineBridge",
    }

    static #host = {
        /** @type {RuntimeAPI} */
        api: undefined,
        exports: undefined,
        /** @type {MonoConfig} */
        monoConfig: undefined,
    }

    /** @type {{sim: Float64Array, body: Float64Array}} */
    static #bufferView = {
        sim: undefined,
        body: undefined,
    }

    /** @type {{sim: Record<string, number>, body: Record<string, number>}} */
    static #layoutRecord = {
        sim: undefined,
        body: undefined,
    }

    static #EngineBridge;

    //#region Utils

    static #log(msg, ...args) {
        if(Bridge.#CONFIG.DEBUG_MODE) console.log(msg, ...args);
    }

    /**
     * @param {string[]} layoutArr
     * @returns {Record<string, number>}
     */
    static #getStateLayoutRecord(layoutArr) {
        const record = {};
        for(let i = 0; i < layoutArr.length; i++) {
            record[layoutArr[i]] = i;
        }
        return record;
    }

    //#endregion


    //#region API

    /** @returns {SimState} */
    static get simState() { 
        return this.#simState;
    }

    /**
     * Initializes the Bridge. Must be called first!
     * @param {boolean} [debugMode=false]           If true enables debug logging and exposes the `EngineBridge` to the `globalThis`.
     * @param {boolean} [diagnosticsTracing=false]  If "true", writes diagnostic messages during runtime startup and execution to the browser console.
     * @returns {Promise<void>}
     */
    static async initialize(debugMode=false, diagnosticsTracing=false) {
        Bridge.#CONFIG.DEBUG_MODE = !!debugMode;
        const {NAMESPACE, CLASS_NAME, DEBUG_MODE} = Bridge.#CONFIG;
        if(Bridge.#host.api) throw new Error("Bridge has already been initialized.");

        const builder = dotnet.withDiagnosticTracing(!!diagnosticsTracing);

        Bridge.#host.api = await builder.create();
        Bridge.#host.monoConfig = Bridge.#host.api.getConfig();
        Bridge.#host.exports = await Bridge.#host.api.getAssemblyExports(Bridge.#host.monoConfig.mainAssemblyName);
        Bridge.#EngineBridge = Bridge.#host.exports[NAMESPACE][CLASS_NAME];

        Bridge.#layoutRecord.sim = Bridge.#getStateLayoutRecord(this.#EngineBridge.GetSimStateLayout());
        Bridge.#layoutRecord.body = Bridge.#getStateLayoutRecord(this.#EngineBridge.GetBodyStateLayout());

        Bridge.#refreshSimState();

        if(DEBUG_MODE) globalThis.EngineBridge = this;
    }

    /**
     * Ticks the engine and refreshes the simState data.
     * @param {number} timestamp The timestamp from `requestAnimationFrame()`
     * @returns {BodyDiffData} 
     * @throws Error if the EngineBridge returns an error message.
     */
    static tickEngine(timestamp) {
        const errorMsg = this.#EngineBridge.Tick(timestamp);
        if(errorMsg) throw new Error(errorMsg);
        return this.#refreshSimState();
    }

    /**
     * 
     * @param {number} bodyCount 
     * @returns {void}
     */
    static _createTestSim(bodyCount) {
        return this.#EngineBridge.CreateTestSim(bodyCount);
    }

    /**
     * Serializes the current state of the physics engine simulation into a JSON string.
     * @returns {string} A string containing the simulation state in JSON format.
     */
    static getPreset() {
        return this.#EngineBridge.GetPreset();
    }

    /**
     * Loads a preset string into the engine and refreshes simState data.
     * @param {string} jsonPreset A string containing the simulation state in JSON format.
     * @returns {BodyDiffData}
     * @throws Error if the EngineBridge returns an error message.
     */
    static loadPreset(jsonPreset) {
        const errorMsg = this.#EngineBridge.LoadPreset(jsonPreset);
        if(errorMsg) throw new Error(errorMsg);
        return this.#refreshSimState();
    }

    /**
     * Creates a new body with default properties.
     * @returns {number} Body Id
     */
    static createBody() {
        return this.#EngineBridge.CreateBody();
    }

    /**
     * Deletes an existing body.
     * @param {number} id Body Id
     * @returns {boolean} True if the body was deleted, false if it wasn't found.
     */
    static deleteBody(id) {
        return this.#EngineBridge.DeleteBody(id);
    }

    /**
     * Updates an existing body.
     * @param {number} id  Body Id
     * @param {Partial<{
     *  enabled: boolean|number,
     *  mass: number,
     *  posX: number,
     *  posY: number,
     *  velX: number,
     *  velY: number
     * }>} values        The new values for the body
     * @returns {boolean} True if the body has been updated successfully, false if not found.
     */
    static updateBody(id, { enabled, mass, posX, posY, velX, velY }={}) {
        const body = this.#simState.bodies.get(id);
        if(!body) return false;

        // Avoid unnessecary WASM call if possible
        const isUnchanged = (enabled === undefined || !!enabled === !!body.enabled)
            && (mass === undefined || mass === body.mass)
            && (posX === undefined || posX === body.posX)
            && (posY === undefined || posY === body.posY)
            && (velX === undefined || velX === body.velX)
            && (velY === undefined || velY === body.velY);
        if(isUnchanged) return true;

        return this.#EngineBridge.UpdateBody(id, 
            !!(enabled ?? body.enabled),     // always coerce into boolean!
            mass ?? body.mass,
            posX ?? body.posX,
            posY ?? body.posY,
            velX ?? body.velX,
            velY ?? body.velY
        );
    }

    //#endregion


    //#region Buffer Views

    /** @type {{simPtr: number, simSize: number, bodyPtr: number, bodySize: number}} */
    static #bufferCache = {
        simPtr: null,
        simSize: null,
        bodyPtr: null,
        bodySize: null,
    };

    /**
     * Sets the views into the shared buffers based on the pointers and sizes from the EngineBridge.
     * Compares the cached pointers and sizes to the values received and only updates them and the cache if they're different.
     * @returns {void}
     */
    static #setBufferViews() {
        /* -- Sim Buffer -- */
        // Get ptr and size of sim buffer from the C# bridge
        const [newSimPtr, newSimSize] = this.#EngineBridge.GetSimBufferPtrAndSize();

        // Update sim buffer view only if stale
        const refreshSimBuffer = this.#bufferView.sim?.buffer?.detached
            || this.#bufferCache.simPtr !== newSimPtr
            || this.#bufferCache.simSize !== newSimSize;

        if(refreshSimBuffer) {
            if(typeof newSimPtr !== "number" || newSimPtr === 0) throw new Error(`Invalid simBufferPtr=${newSimPtr}`);
            if(typeof newSimSize !== "number" || newSimSize === 0) throw new Error(`Invalid simBufferSize=${newSimSize}`);

            this.#log(`Updating SimStateBuffer: Pointer=${newSimPtr}, Size=${newSimSize} bytes`);
            
            this.#bufferView.sim = new Float64Array(this.#host.api.localHeapViewU8().buffer, newSimPtr, newSimSize / Float64Array.BYTES_PER_ELEMENT);
            this.#bufferCache.simPtr = newSimPtr;
            this.#bufferCache.simSize = newSimSize;
        }

        
        /* -- Body Buffer -- */
        // Get ptr and size of body buffer from the sim buffer
        const newBodyPtr = this.#bufferView.sim[this.#layoutRecord.sim._bodyBufferPtr];
        const newBodySize = this.#bufferView.sim[this.#layoutRecord.sim._bodyBufferSize];

        // Update body buffer view only if stale
        const refreshBodyBuffer = this.#bufferView.body?.buffer?.detached
            || this.#bufferCache.bodyPtr !== newBodyPtr 
            || this.#bufferCache.bodySize !== newBodySize;
        
        if(refreshBodyBuffer) {
            if(typeof newBodyPtr !== "number" || newBodyPtr === 0) throw new Error(`Invalid bodyBufferPtr=${newBodyPtr}`);
            if(typeof newBodySize !== "number" || newBodySize === 0) throw new Error(`Invalid bodyBufferSize=${newBodySize}`);

            this.#log(`Updating SimStateBuffer: Pointer=${newBodyPtr}, Size=${newBodySize} bytes`);
            
            this.#bufferView.body = new Float64Array(this.#host.api.localHeapViewU8().buffer, newBodyPtr, newBodySize / Float64Array.BYTES_PER_ELEMENT);
            this.#bufferCache.bodyPtr = newBodyPtr;
            this.#bufferCache.bodySize = newBodySize;
        }
    }

    //#endregion


    //#region Shared Memory Reader

    /** @type {SimState} */
    static #simState = {
        /** @type {Map<number, BodyStateData>} */
        bodies: new Map()
    }

    static #readerCache = {
        isInitialized: false,
        /** @type {[string, number][]} */
        simKVCache: [],
        /** @type {[string, number][]} */
        bodyKVCache: [],
        /** @type {number} */
        bodyStride: undefined,
        /** @type {number} */
        idIndex: undefined,
        prevKnownIds: new Set(),
        knownIds: new Set(),
    };

    static #diffCache = {
        created: new Set(),
        updated: new Set(),
        deleted: new Set(),
    }

    static #refreshSimState() {
        this.#setBufferViews();

        // Ensure reader cache values are properly initialized
        if(!this.#readerCache.isInitialized) this.#initReader();

        // Read public data from sim buffer
        for(const [key, index] of this.#readerCache.simKVCache) {
            this.#simState[key] = this.#bufferView.sim[index];
        }

        // Reset diff cache
        const { created, updated, deleted } = this.#diffCache;
        created.clear();
        updated.clear();
        deleted.clear();

        // Swap roles to avoid reallocation
        const { knownIds, prevKnownIds } = this.#readerCache;
        [this.#readerCache.knownIds, this.#readerCache.prevKnownIds] = [prevKnownIds, knownIds];
        this.#readerCache.knownIds.clear();

        
        // Read body buffer data
        for(let i = 0; i < this.#simState.bodyCount; i++) {
            const offset = i * this.#readerCache.bodyStride;
            const id = this.#bufferView.body[offset + this.#readerCache.idIndex];

            knownIds.add(id);

            let body = this.#simState.bodies.get(id);
            let wasCreated = false;
            if(!body) {
                body = {};
                this.#simState.bodies.set(id, body);
                created.add(id);
                wasCreated = true;
            }; 
            
            let wasChanged = false;
            for(const [key, index] of this.#readerCache.bodyKVCache) {
                if(!wasChanged && body[key] !== this.#bufferView.body[offset + index]) {
                    wasChanged = true;
                }
                body[key] = this.#bufferView.body[offset + index];
            }
            if(wasChanged && !wasCreated) updated.add(id);
        }

        // Handle the actual deletion of bodies that were in the previous 
        // frame but are missing from the current frame.
        for(const id of prevKnownIds) {
            if(!knownIds.has(id)) {
                this.#simState.bodies.delete(id);
                deleted.add(id);
            }
        }

        return this.#diffCache;
    }

    static #initReader() {
        for(const [key, index] of Object.entries(this.#layoutRecord.sim)) {
            // Don't expose internal keys in the final public simState object
            if(key.startsWith("_")) continue;
            this.#readerCache.simKVCache.push([key, index]);
        }

        this.#readerCache.bodyKVCache = Object.entries(this.#layoutRecord.body);
        this.#readerCache.bodyStride = this.#readerCache.bodyKVCache.length;
        this.#readerCache.idIndex = this.#layoutRecord.body.id;

        this.#readerCache.isInitialized = true;
    }

    //#endregion
}
