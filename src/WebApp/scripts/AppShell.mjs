// @ts-ignore
import _Bridge from '../bridge/Bridge.mjs';     // created during build
import AppDataManager from './AppDataManager.mjs';
import CanvasView from './components/CanvasView.mjs';
import Notifications from './components/Notifications.mjs';

/**
 * @import { BodyStateData } from '../types/Bridge'
 * @import { BodyMetaData } from './AppDataManager.mjs'
 */

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
        this.canvasView = await CanvasView.create();

        if(this.#CONFIG.debugMode) globalThis.AppShell = this;
        console.log("Initialization complete.");
    }

    //#region Controls

    /**
     * 
     * @returns {number}
     */
    static createBody() {
        const id = this.Bridge.createBody();
        this.appDataManager._onCreateBody(id);
        return id;
    }

    /**
     * 
     * @param {number} id 
     * @returns {boolean}
     */
    static deleteBody(id) {
        const ret = this.Bridge.deleteBody(id);
        this.appDataManager._onDeleteBody(id);
        return ret;
    }

    /**
     * 
     * @param {number} id 
     * @param {Partial<BodyStateData & BodyMetaData>} updates 
     */
    static updateBody(id, updates={}) {
        const bridgeSuccess = this.Bridge.updateBody(id, updates);
        if(!bridgeSuccess) return false;

        const appDataSuccess = this.appDataManager._onUpdateBody(id, updates);
        if(!appDataSuccess) throw new Error(`Body id "${id}" in sim data but not in appData.`);

        this.canvasView.queueBodyUpdate(id);

        return true;
    }

    //#endregion

    //#region Render Loop Control

    static stopLoop() { this.canvasView.toggleStop(true); }

    static startLoop() { this.canvasView.toggleStop(false); }

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
