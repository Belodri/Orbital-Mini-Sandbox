// @ts-ignore
import _Bridge from '../bridge/bridge.mjs';     // created during build
import AppDataManager from './AppDataManager.mjs';
import CanvasView from './components/CanvasView.mjs';
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

    /** @type {AppDataManager} */
    static appDataManager;

    /** @type {CanvasView} */
    static canvasView;

    //#endregion

    static async initialize() {
        console.log("Begin initialization.");

        this.log("Initializing Bridge...");
        await this.Bridge.initialize(); 

        this.log("Instantiating AppDataManager...");
        this.appDataManager = new AppDataManager();

        this.log("Instantiating CanvasView...");
        this.canvasView = new CanvasView();

        if(this.#CONFIG.debugMode) globalThis.AppShell = this;
        console.log("Initialization complete.");
    }

    //#region Controls

    /**
     * 
     * @param {boolean} [force=undefined] 
     * @returns {void}
     */
    static togglePause(force=undefined) {
        let newState = force === undefined
            ? !this.canvasView.isPaused
            : force;

        if(newState) this.canvasView.pause();
        else this.canvasView.unpause();
    }

    static createBody() {
        const id = this.Bridge.createBody();
        this.appDataManager._onBodyCreated(id);
        return id;
    }

    //#endregion

    //#region Utility

    /**
     * 
     * @param {string} msg 
     * @param {any} data
     * @returns {void}
     */
    static log(msg, data) {
        if(this.#CONFIG.debugMode) {
            if(data) console.log(msg, data);
            else console.log(msg);
        }
    }

    //#endregion
}
