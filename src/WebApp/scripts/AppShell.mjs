// @ts-ignore
import _Bridge from '../bridge/bridge.mjs';     // created during build
import Notifications from './components/Notifications.mjs';

export default class AppShell {
    static #CONFIG = {
        debugMode: true,
    }

    //#region Components

    /** @type {typeof import("../types/Bridge").default} */
    static Bridge = _Bridge;

    /** @type {Notifications} */
    static notifications = new Notifications();

    //#endregion

    static async initialize() {
        console.log("Begin initialization.")

        this.log("Initializing Bridge...")
        await this.Bridge.initialize(); 

        if(this.#CONFIG.debugMode) globalThis.AppShell = this;
        console.log("Initialization complete.")
    }

    /**
     * 
     * @param {string} msg 
     * @param {any} data
     * @returns {void}
     */
    static log(msg, data) {
        if(this.#CONFIG.debugMode) console.log(msg, data);
    }
}
