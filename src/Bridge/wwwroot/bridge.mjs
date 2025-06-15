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
        METHODS_SYNC: [
            "GetSimStateBufferPtr",
            "GetSimStateBufferSize",
            "GetSimStateLayout",
            "GetBodyStateLayout",
        ],
        METHODS_ASYNC: [
            "SetTestString",
            "GetTestString"
        ]
    }

    static log(msg, ...args) {
        if(Bridge.#CONFIG.DEBUG_MODE) console.log(msg, ...args);
    }

    /** @type {RuntimeAPI} */
    static #api;

    static #exports;

    /** @type {MonoConfig} */
    static #monoConfig;

    static #EngineBridge;

    /** @type {Float64Array} */
    static #simBufferView;

    /** @type {Float64Array} */
    static #bodyStateBufferView;

    /** @type {Record<string, [index: number]>} */
    static #bodyStateLayoutRecord;

    /** @type {Record<string, [index: number]>} */
    static #simStateLayoutRecord;

    //#region Initialization

    static async initialize() {
        const {NAMESPACE, CLASS_NAME, DEBUG_MODE} = Bridge.#CONFIG;
        if(Bridge.#api) throw new Error("Bridge has already been initialized.");

        Bridge.#api = await dotnet.create();
        Bridge.#monoConfig = Bridge.#api.getConfig();
        Bridge.#exports = await Bridge.#api.getAssemblyExports(Bridge.#monoConfig.mainAssemblyName);
        Bridge.#EngineBridge = Bridge.#exports[NAMESPACE][CLASS_NAME];

        Bridge.#simStateLayoutRecord = Bridge.#getStateLayoutRecord(Bridge.callSync("GetSimStateLayout"));
        Bridge.#bodyStateLayoutRecord = Bridge.#getStateLayoutRecord(Bridge.callSync("GetBodyStateLayout"));

        Bridge.#setSimStateBufferView();
        Bridge.#setBodyStateBufferView()

        if(DEBUG_MODE) globalThis.EngineBridge = this;
        return true;
    }

    /**
     * @param {string[]} layoutArr
     * @returns {Record<string, [index: number]>}
     */
    static #getStateLayoutRecord(layoutArr) {
        const record = {};
        for(let i = 0; i < layoutArr.length; i++) {
            record[layoutArr[i]] = i;
        }
        return record;
    }

    //#endregion


    //#region Temporary C# method call utils

    static callSync(name, ...args) {
        if(!Bridge.#CONFIG.METHODS_SYNC.includes(name)) {
            throw new Error(`Method "${name}" is not in the configured list of sync methods.`);
        }

        const method = Bridge.#getMethod(name);
        return method(...args);
    }

    /*
        Turn some calls into a promise just so they're put on the task stack and allow other, 
        more pressing code to run before.
    */
    static callAsync(name, ...args) {
        if(!Bridge.#CONFIG.METHODS_ASYNC.includes(name)) {
            throw new Error(`Method "${name}" is not in the configured list of async methods.`);
        }

        const method = Bridge.#getMethod(name);
        return new Promise((resolve, reject) => {
                setTimeout(() => {
                    try {
                        const result = method(...args);
                        resolve(result);
                    } catch (err) {
                        reject(err);
                    }
                }, 0);
            });
    }

    /**
     * @param {string} name 
     * @returns {Function}
     */
    static #getMethod(name) {
        const method = this.#EngineBridge[name];
        if(!method) throw new Error(`Method "${name}" not found in C# class "${Bridge.#CONFIG.CLASS_NAME}"`);
        return method;
    }

    //#endregion

    static #setSimStateBufferView() {
        const simStatePtr = this.callSync("GetSimStateBufferPtr");
        const simStateSize = this.callSync("GetSimStateBufferSize");

        this.log(`Received SimStateBuffer info: Pointer=${simStatePtr}, Size=${simStateSize} bytes`);

        const wasmHeap = this.#api.localHeapViewU8().buffer;
        const arrayLength = simStateSize / Float64Array.BYTES_PER_ELEMENT;
        this.#simBufferView = new Float64Array(wasmHeap, simStatePtr, arrayLength);

        return this;
    }

    /**
     * Sets the view into the bodyStateBuffer based on the pointers and size in simStateBuffer.
     * @returns {this}
     */
    static #setBodyStateBufferView() {
        if(!this.#simBufferView) throw new Error(`simBufferView not initialized.`);

        const ptr = this.#simBufferView[Bridge.#simStateLayoutRecord["bodyBufferPtr"]];
        const size = this.#simBufferView[Bridge.#simStateLayoutRecord["bodyBufferSize"]];   

        this.log(`Received BodyStateBuffer info: Pointer=${ptr}, Size=${size} bytes`);

        if(typeof ptr !== "number" || ptr === 0) throw new Error(`Invalid bodyBufferPtr=${ptr}`);
        if(typeof size !== "number" || size === 0) throw new Error(`Invalid bodyBufferSize=${ptr}`);

        const wasmHeap = this.#api.localHeapViewU8().buffer;
        const arrayLength = size / Float64Array.BYTES_PER_ELEMENT;
        this.#bodyStateBufferView = new Float64Array(wasmHeap, ptr, arrayLength);

        return this;
    }
}
