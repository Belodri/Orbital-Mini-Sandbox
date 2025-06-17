import { dotnet as _dotnet } from './_framework/dotnet.js';
/** @type {import('../../Bridge/types/dotnet.js').dotnet} */
const dotnet = _dotnet;

/**
 * @import { MonoConfig, RuntimeAPI,  } from '../types/dotnet.js'
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

    /** @type {{ptr: number, size: number}} */
    static #bodyBufferPtrCache = {
        ptr: undefined,
        size: undefined
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

    // TODO Write a Bridge.d.ts file for the public facing API
    // See if the definitions for BodyStateData and SimState could be auto-generated


    /**
     * @typedef {{id: number, [key: string]: number}} BodyStateData
     */

    /**
     * @typedef {object} SimState
     * @property {Map<number, BodyStateData>} bodies
     * @property {number} bodyCount
     * @property {number} [key] 
     */

    /** @returns {SimState | undefined} */
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

        Bridge.#setSimStateBufferView();
        Bridge.#setBodyStateBufferView();

        if(DEBUG_MODE) globalThis.EngineBridge = this;
    }


    /**
     * @typedef {object} BodyDiffData
     * @property {Set<number>} created      The ids of newly created bodies
     * @property {Set<number>} updated      The ids of bodies that were neither created nor destroyed
     * @property {Set<number>} deleted      The ids of deleted bodies
     */

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

    //#endregion


    //#region Buffer Views

    static #setSimStateBufferView() {
        const simStatePtr = this.#GetSimStateBufferPtr();
        const simStateSize = this.#GetSimStateBufferSize();

        this.#log(`Received SimStateBuffer info: Pointer=${simStatePtr}, Size=${simStateSize} bytes`);

        const wasmHeap = this.#host.api.localHeapViewU8().buffer;
        const arrayLength = simStateSize / Float64Array.BYTES_PER_ELEMENT;
        this.#bufferView.sim = new Float64Array(wasmHeap, simStatePtr, arrayLength);

        return this;
    }

    /**
     * Sets the view into the bodyStateBuffer based on the pointers and size in simStateBuffer.
     * Compares the cached ptr and size to the values in the bufferView.sim and only updates if they're different and updates the cache.
     * @returns {this}
     */
    static #setBodyStateBufferView() {
        if(!this.#bufferView.sim) throw new Error(`simBufferView not initialized.`);

        const ptr = this.#bufferView.sim[Bridge.#layoutRecord.sim["_bodyBufferPtr"]];
        const size = this.#bufferView.sim[Bridge.#layoutRecord.sim["_bodyBufferSize"]];

        if(this.#bodyBufferPtrCache.ptr === ptr && this.#bodyBufferPtrCache.size === size) {
            return this;
        }

        if(typeof ptr !== "number" || ptr === 0) throw new Error(`Invalid _bodyBufferPtr=${ptr}`);
        if(typeof size !== "number" || size === 0) throw new Error(`Invalid _bodyBufferSize=${size}`);

        const wasmHeap = this.#host.api.localHeapViewU8().buffer;
        const arrayLength = size / Float64Array.BYTES_PER_ELEMENT;
        this.#bufferView.body = new Float64Array(wasmHeap, ptr, arrayLength);

        this.#bodyBufferPtrCache.ptr = ptr;
        this.#bodyBufferPtrCache.size = size;

        return this;
    }

    //#endregion


    //#region Shared Memory Reader

    static #simState = {
        /** @type {Map<number, BodyStateData>} */
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

        this.#setBodyStateBufferView();

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
