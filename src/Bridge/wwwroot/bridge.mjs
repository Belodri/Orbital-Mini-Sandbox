import { dotnet as _dotnet } from './_framework/dotnet.js';
/** @type {import('../../Bridge/types/dotnet.js').dotnet} */
const dotnet = _dotnet;

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

    /** @returns {import('../types/bridge.js').SimState} */
    static get simState() { 
        return this.#simState;
    }

    /**
     * Initializes the Bridge. Must be called first!
     * @returns {Promise<void>}
     */
    static async initialize() {
        const {NAMESPACE, CLASS_NAME, DEBUG_MODE} = Bridge.#CONFIG;
        if(Bridge.#host.api) throw new Error("Bridge has already been initialized.");

        Bridge.#host.api = await dotnet.create();
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
     * @returns {import('../types/bridge.js').BodyDiffData} 
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
     * @returns {import('../types/bridge.js').BodyDiffData}
     * @throws Error if the EngineBridge returns an error message.
     */
    static loadPreset(jsonPreset) {
        const errorMsg = this.#EngineBridge.LoadPreset(jsonPreset);
        if(errorMsg) throw new Error(errorMsg);
        return this.#refreshSimState();
    }

    /**
     * 
     * @returns {number} Body Id
     */
    static createBody() {
        return this.#EngineBridge.CreateBody();
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
        // Get ptr and size of sim buffer from the C# bridge
        const simBufferData = this.#EngineBridge.GetSimBufferPtrAndSize();
        const newSimPtr = simBufferData[0];
        const newSimSize = simBufferData[1];
        this.#setSimBufferView(newSimPtr, newSimSize);

        // Get ptr and size of body buffer from the sim buffer
        const newBodyPtr = this.#bufferView.sim[this.#layoutRecord.sim._bodyBufferPtr];
        const newBodySize = this.#bufferView.sim[this.#layoutRecord.sim._bodyBufferSize];
        this.#setBodyBufferView(newBodyPtr, newBodySize);
    }

    /**
     * Sets the views into the shared sim buffer.
     * @param {number} ptr      The byteOffset
     * @param {number} size     The buffer size in bytes
     * @returns {void}
     */
    static #setSimBufferView(ptr, size) {
        // Update buffer view only if stale
        if(this.#bufferCache.simPtr === ptr && this.#bufferCache.simSize === size) return this;

        if(typeof ptr !== "number" || ptr === 0) throw new Error(`Invalid simBufferPtr=${ptr}`);
        if(typeof size !== "number" || size === 0) throw new Error(`Invalid simBufferSize=${size}`);

        this.#log(`Updating SimStateBuffer: Pointer=${ptr}, Size=${size} bytes`);
        
        this.#bufferView.sim = new Float64Array(this.#host.api.localHeapViewU8().buffer, ptr, size / Float64Array.BYTES_PER_ELEMENT);
        this.#bufferCache.simPtr = ptr;
        this.#bufferCache.simSize = size;
    }

    /**
     * Sets the views into the shared body buffer.
     * @param {number} ptr      The byteOffset
     * @param {number} size     The buffer size in bytes
     * @returns {void}
     */
    static #setBodyBufferView(ptr, size) {
        // Update buffer view only if stale
        if(this.#bufferCache.bodyPtr === ptr && this.#bufferCache.bodySize === size) return this;

        if(typeof ptr !== "number" || ptr === 0) throw new Error(`Invalid bodyBufferPtr=${ptr}`);
        if(typeof size !== "number" || size === 0) throw new Error(`Invalid bodyBufferSize=${size}`);

        this.#log(`Updating BodyStateBuffer: Pointer=${ptr}, Size=${size} bytes`);

        this.#bufferView.body = new Float64Array(this.#host.api.localHeapViewU8().buffer, ptr, size / Float64Array.BYTES_PER_ELEMENT);
        this.#bufferCache.bodyPtr = ptr;
        this.#bufferCache.bodySize = size;
    }

    //#endregion


    //#region Shared Memory Reader

    /** @type {import('../types/bridge.js').SimState} */
    static #simState = {
        /** @type {Map<number, import('../types/bridge.js').BodyStateData>} */
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
    };

    static #refreshSimState() {
        this.#setBufferViews();

        // Ensure reader cache values are properly initialized
        if(!this.#readerCache.isInitialized) this.#initReader();

        // Read public data from sim buffer
        for(const [key, index] of this.#readerCache.simKVCache) {
            this.#simState[key] = this.#bufferView.sim[index];
        }

        // Read body buffer data
        const createdIds = new Set();
        const updatedIds = new Set();
        const excessIds = new Set(this.#simState.bodies.keys());    // Get all current keys

        for(let i = 0; i < this.#simState.bodyCount; i++) {
            const offset = i * this.#readerCache.bodyStride;
            const id = this.#bufferView.body[offset + this.#readerCache.idIndex];
            excessIds.delete(id);

            let body = this.#simState.bodies.get(id);
            let created = false;
            if(!body) {
                body = {};
                this.#simState.bodies.set(id, body);
                createdIds.add(id);
                created = true;
            }; 
            
            let changed = false;
            for(const [key, index] of this.#readerCache.bodyKVCache) {
                if(!changed && body[key] !== this.#bufferView.body[offset + index]) {
                    changed = true;
                }
                body[key] = this.#bufferView.body[offset + index];
            }
            if(changed && !created) updatedIds.add(id);
        }

        // Remove remaining excess bodies 
        for(const id of excessIds) this.#simState.bodies.delete(id);

        return {
            created: createdIds,
            deleted: excessIds,
            updated: updatedIds,
        }
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
