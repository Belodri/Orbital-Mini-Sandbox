import { dotnet as _dotnet } from './_framework/dotnet.js';
/** @type {import('../../Bridge/types/dotnet.js').dotnet} */
const dotnet = _dotnet;

/**
 * @import { MonoConfig, RuntimeAPI,  } from '../../Bridge/types/dotnet.js'
 */

export default class Bridge {
    static #CONFIG = {
        DEBUG_MODE: true,
        NAMESPACE: "Bridge",
        CLASS_NAME: "EngineBridge",
        METHODS_SYNC: [
            
        ],
        METHODS_ASYNC: [
            "GetSimStateLayout",
            "GetBodyStateLayout",
            "SetTestString",
            "GetTestString"
        ],
    }

    /** @type {RuntimeAPI} */
    static #api;

    static #exports;

    /** @type {MonoConfig} */
    static #monoConfig;

    static #EngineBridge;

    static async initialize() {
        const {NAMESPACE, CLASS_NAME, DEBUG_MODE} = Bridge.#CONFIG;
        if(Bridge.#api) throw new Error("Bridge has already been initialized.");

        Bridge.#api = await dotnet.create();
        Bridge.#monoConfig = Bridge.#api.getConfig();
        Bridge.#exports = await Bridge.#api.getAssemblyExports(Bridge.#monoConfig.mainAssemblyName);
        Bridge.#EngineBridge = Bridge.#exports[NAMESPACE][CLASS_NAME];

        if(DEBUG_MODE) globalThis.EngineBridge = this;

        return true;
    }

    static callSync(name, ...args) {
        if(!Bridge.#CONFIG.METHODS_SYNC.includes(name)) {
            throw new Error(`Method "${name}" is not in the configured list of sync methods.`);
        }

        const method = Bridge.#getMethod(name);
        return method(...args);
    }

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

}
