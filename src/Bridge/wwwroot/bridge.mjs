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

    /** @returns {import('../types/bridge.js').SimState | undefined} */
    static get simState() { 
        if(!this.#simStateReady) return undefined;
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

        Bridge.#layoutRecord.sim = Bridge.#getStateLayoutRecord(Bridge.#GetSimStateLayout());
        Bridge.#layoutRecord.body = Bridge.#getStateLayoutRecord(Bridge.#GetBodyStateLayout());

        Bridge.#setBufferViews();

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
        if(this.#CONFIG.DEBUG_MODE) {
            const heapBefore = this.#host.api.localHeapViewU8().buffer;
            console.log("Heap ArrayBuffer before call:", heapBefore);
        }
        
        const errorMsg = this.#EngineBridge.LoadPreset(jsonPreset);
        if(errorMsg) throw new Error(errorMsg);

        if(this.#CONFIG.DEBUG_MODE) {
            const heapAfter = this.#host.api.localHeapViewU8().buffer;
            console.log("Heap ArrayBuffer after call:", heapAfter);
            console.log("Did heap change?", heapBefore !== heapAfter); // This will likely be TRUE
        }

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


    //#region C# Methods - Private 

    /**
     * 
     * @returns {number}
     */
    static #GetSimStateBufferPtr() {
        return this.#EngineBridge.GetSimStateBufferPtr();
    }

    /**
     * 
     * @returns {number}
     */
    static #GetSimStateBufferSize() {
        return this.#EngineBridge.GetSimStateBufferSize();
    }

    /**
     * 
     * @returns {string[]}
     */
    static #GetSimStateLayout() {
        return this.#EngineBridge.GetSimStateLayout();
    }

    /**
     * 
     * @returns {string[]}
     */
    static #GetBodyStateLayout() {
        return this.#EngineBridge.GetBodyStateLayout();
    }

    /**
     * 
     * @returns {number[]}  simStateBufferPtr, sumStateBufferSize, bodyBufferPtr, bodyBufferSize
     */
    static #GetBufferData() {
        return this.#EngineBridge.GetBufferData();
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
     * @returns {this}
     */
    static #setBufferViews() {
        const bufferData = this.#GetBufferData();
        // TODO Replace direct access with dynamic layout
        const newSimPtr = bufferData[0];
        const newSimSize = bufferData[1];
        const newBodyPtr = bufferData[2];
        const newBodySize = bufferData[3];

        const wasmHeap = this.#host.api.localHeapViewU8().buffer;

        if(this.#bufferCache.simPtr !== newSimPtr || this.#bufferCache.simSize !== newSimSize) {
            if(typeof newSimPtr !== "number" || newSimPtr === 0) throw new Error(`Invalid simBufferPtr=${newSimPtr}`);
            if(typeof newSimSize !== "number" || newSimSize === 0) throw new Error(`Invalid simBufferSize=${newSimSize}`);

            this.#log(`Updating SimStateBuffer: Pointer=${newSimPtr}, Size=${newSimSize} bytes`);

            this.#bufferView.sim = new Float64Array(wasmHeap, newSimPtr, newSimSize / Float64Array.BYTES_PER_ELEMENT);
            this.#bufferCache.simPtr = newSimPtr;
            this.#bufferCache.simSize = newSimSize;
        }

        if(this.#bufferCache.bodyPtr !== newBodyPtr || this.#bufferCache.bodySize !== newBodySize) {
            if(typeof newBodyPtr !== "number" || newBodyPtr === 0) throw new Error(`Invalid bodyBufferPtr=${newBodyPtr}`);
            if(typeof newBodySize !== "number" || newBodySize === 0) throw new Error(`Invalid bodyBufferSize=${newBodySize}`);

            this.#log(`Updating BodyStateBuffer: Pointer=${newBodyPtr}, Size=${newBodySize} bytes`);

            this.#bufferView.body = new Float64Array(wasmHeap, newBodyPtr, newBodySize / Float64Array.BYTES_PER_ELEMENT);
            this.#bufferCache.bodyPtr = newBodyPtr;
            this.#bufferCache.bodySize = newBodySize;
        }

        return this;
    }

    //#endregion


    //#region Shared Memory Reader

    /** @type {import('../types/bridge.js').SimState} */
    static #simState = {
        /** @type {Map<number, import('../types/bridge.js').BodyStateData>} */
        bodies: new Map()
    }

    static #simStateReady = false;

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
        if (!this.#bufferView.sim || !this.#bufferView.body) {
            console.error("Shared memory buffers not initialized. Cannot get state.");
            return null;
        }

        this.#setBufferViews();

        // Ensure reader cache values are properly initialized
        if(!this.#readerCache.isInitialized) this.#initReader();

        // Read sim buffer data
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
            if(!body) {
                body = {};
                this.#simState.bodies.set(id, body);
                createdIds.add(id);
            } else updatedIds.add(id);

            for(const [key, index] of this.#readerCache.bodyKVCache) {
                body[key] = this.#bufferView.body[offset + index];
            }
        }

        // Remove remaining excess bodies 
        for(const id of excessIds) this.#simState.bodies.delete(id);

        // Allow simState to be read by consumers
        this.#simStateReady = true;

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
