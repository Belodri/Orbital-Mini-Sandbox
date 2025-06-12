import { dotnet as _dotnet } from '../_framework/dotnet.js';
/** @type {import('../types/dotnet.d.ts').dotnet} */
const dotnet = _dotnet;

/**
 * @import { MonoConfig, RuntimeAPI,  } from '../types/dotnet.d.ts'
 */

class Bridge {
    static CONFIG = Object.freeze({
        NAMESPACE: "Bridge",
        CLASS_NAME: "EngineBridge",
        STRICT_MODE: true,
        METHODS_SYNC: Object.freeze([
            "Tick"
        ]),
        METHODS_ASYNC: Object.freeze([
            "GetTickErrorText"
        ]),
    });

    /** @type {Bridge} */
    static #handler;

    static get handler() { return Bridge.#handler; }

    /**
     * 
     * @returns {Promise<Bridge>}
     */
    static async initialize() {
        if(Bridge.handler) throw new Error("An instance of BridgeHandler already exists.");
        const runtimeAPI = await dotnet.create();
        const config = runtimeAPI.getConfig();
        const exports = await runtimeAPI.getAssemblyExports(config.mainAssemblyName);
        const handler = new Bridge(runtimeAPI, config, exports);
        Bridge.#handler = handler;
        return Bridge.handler;
    }

    static checkAsync(methodName) {
        return Bridge.CONFIG.METHODS_ASYNC.includes(methodName);
    }

    static callMethod(name, ...args) {
        return Bridge.#handler.callMethod(name, ...args);
    }

    /** @type {RuntimeAPI} */
    #api;

    #exports;

    /** @type {MonoConfig} */
    #monoConfig;

    /** @type {Map<string, Function>} */
    #methods = new Map();

    #EngineBridge;

    constructor(runtimeAPI, config, exports) {
        if(Bridge.handler) throw new Error("An instance of BridgeHandler already exists.");
        this.#api = runtimeAPI;
        this.#monoConfig = config;
        this.#exports = exports;

        const {NAMESPACE, CLASS_NAME} = Bridge.CONFIG;
        this.#EngineBridge = exports[NAMESPACE][CLASS_NAME];
    }

    callMethod(name, ...args) {
        if(Bridge.CONFIG.STRICT_MODE) {
            const isKnown = Bridge.CONFIG.METHODS_SYNC.includes(name) 
                || Bridge.CONFIG.METHODS_ASYNC.includes(name);
            if(!isKnown) throw new Error(`Method "${name}" is not in the configured method list.`);
        }

        const method = this.#EngineBridge[name];
        if(!method) throw new Error(`Method "${name}" not found in C# class "${Bridge.CONFIG.CLASS_NAME}"`);

        const isAsync = Bridge.checkAsync(name);
        if(isAsync) {
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
        } else {
            return method(...args);
        }
    }
}

async function initialize() {
    await Bridge.initialize("EngineBridge");
    await test();
}

async function test() {
    let tickErrText;

    console.log("--- Test Run ---");

    tickErrText = await Bridge.callMethod("GetTickErrorText");
    console.log(`first InvokeCall: ${tickErrText}`);    // expected ""

    Bridge.callMethod("Tick");

    tickErrText = await Bridge.callMethod("GetTickErrorText");
    console.log(`second InvokeCall: ${tickErrText}`); // expected "TESTING"

    Bridge.callMethod("Tick");

    tickErrText = await Bridge.callMethod("GetTickErrorText");
    console.log(`third InvokeCall: ${tickErrText}`); // expected ""

    console.log("--- Test Complete ---");
}

try {
    await initialize();
} catch(err) {
    console.error(err);
}
