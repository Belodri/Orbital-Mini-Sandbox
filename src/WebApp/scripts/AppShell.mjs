// @ts-ignore
import _Bridge from '../bridge/Bridge.mjs';     // created during build
import AppDataManager from './AppDataManager.mjs';
import CanvasView from './components/CanvasView.mjs';
import Notifications from './components/Notifications.mjs';

/**
 * @import { BodyDiffData, BodyStateData } from '../types/Bridge'
 * @import { BodyMetaData } from './AppDataManager.mjs'
 */

/** The central orchestrator for the application. */
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

    
    //#region Initialization

    /**
     * Initializes all application components in the correct order  
     * and sets up the necessary event listeners and callbacks.
     * @returns {Promise<void>}
     */
    static async initialize() {
        console.log("Begin initialization.");

        await this.#initBridge();

        this.log("Instantiating AppDataManager...");
        this.appDataManager = new AppDataManager();

        await this.#initCanvasView();

        if(this.#CONFIG.debugMode) {
            globalThis.AppShell = this;
            globalThis.Scenarios = new Scenarios();
        }
        console.log("Initialization complete.");
    }

    /**
     * Initializes the simulation Bridge and registers the `#onStateChange`
     * callback to listen for state updates from the physics engine.
     */
    static async #initBridge() {
        this.log("Initializing Bridge...");
        await this.Bridge.initialize(this.#CONFIG.debugMode, this.#CONFIG.debugMode);

        this.Bridge.registerOnTickCallback(this.#onStateChange, this);
    }

    /**
     * Initializes the CanvasView, injecting the required data stores and callbacks.
     * Registers a callback to the 'renderFrameReady' event, which forms
     * the main application loop for driving the physics engine.
     */
    static async #initCanvasView() {
        this.log("Instantiating CanvasView...");
        this.canvasView = await CanvasView.create({
            bodyMetaDataStore: this.appDataManager.bodyData,
            bodyStateDataStore: this.Bridge.simState.bodies,
            onError: this.onError
        });

        this.canvasView.registerCallback("renderFrameReady", (deltaTime) => {
            if(this.paused) return;

            try {
                this.Bridge.tickEngine(deltaTime);
            } catch (err) {
                this.togglePause(true);
                console.error(err);
                this.notifications.add(`Physics Error.`);
            }
        });
    }

    //#endregion


    //#region Body Controls

    /**
     * Requests the creation of a new body in the simulation.
     * 
     * The promise will resolve once the new body has been created in the physics engine,
     * its state has been propagated back to the application,
     * and its corresponding metadata and visual representation have been initialized.
     * @returns {Promise<number>}   A promise that resolves with the unique ID of the new body.
     */
    static async createBody() {
        return await this.Bridge.createBody();
    }

    /**
     * Requests the deletion of a body from the simulation.
     * 
     * The promise will resolve after the body has been fully removed 
     * from the physics engine, its metadata store, and the renderer.
     * 
     * @param {number} id               The unique ID of the body to delete.
     * @returns {Promise<boolean>}      A promise that resolves with `true` on success, or `false` if it wasn't found.
     */
    static async deleteBody(id) {
        return await this.Bridge.deleteBody(id);
    }

    /**
     * Requests an update for a body's physical state and/or metadata.
     * 
     * This method provides a guarantee of eventual consistency. All updates submitted
     * within the same event loop cycle are merged, with later calls overriding
     * earlier ones for the same properties (last-write-wins).
     * 
     * The returned promise resolves after the body has been updated (or the update has been rejected)
     * in the physics engine and/or metadata store, and the rendered view.
     * 
     * @param {number} id                                       The unique ID of the body to update.
     * @param {Partial<BodyStateData & BodyMetaData>} updates   An object with the properties to change.
     * @returns {Promise<boolean>}  A promise that resolves with `true` if the update was successfully
     *                              processed by the Bridge, or `false` if the body ID was invalid at the time of queuing.
     */
    static async updateBody(id, updates={}) {
        // Try queue the metadata update to happen on the next frame
        const queued = this.appDataManager.queueBodyDataUpdate(id, updates);
        if(!queued) return false;        

        // Tell the Bridge to update the body on the next frame.
        // This promise resolves AFTER the next frame has been rendered!
        const bridgeSuccess = await this.Bridge.updateBody(id, updates);
        if(!bridgeSuccess) {
            // Should never happen under normal circumstances.
            throw new Error(`State Sync Error | Body id=${id} was found in appData but update in engine failed.`);
        }

        return true;
    }

    //#endregion

    //#region Simulation Controls

    /**
     * Requests an update for the simulation state.
     * @param {Parameters<(typeof import('../types/Bridge').default)['updateSimulation']>[0]} updates Partial update data.
     * @returns {Promise<void>} A promise that resolves after the simulation has been updated.
     */
    static async updateSimulation(updates={}) {
        return await this.Bridge.updateSimulation(updates);
    }

    //#endregion


    //#region State Management

    /**
     * The core callback that handles state diffs from the simulation engine.
     * It is called by the Bridge after every physics tick with a list of all
     * bodies that were created, deleted, or updated during that tick. This
     * method then orchestrates the necessary updates across the other app components.
     * @param {BodyDiffData} bodyDiffData   An object containing sets of created, deleted, and updated body IDs.
     */
    static #onStateChange(bodyDiffData) {
        const {created, deleted, updated} = bodyDiffData;

        // Handle creations
        for(const id of created) this.appDataManager.onCreateBody(id);
        this.canvasView.addFrameData("created", created);
        
        // Handle deletions
        for(const id of deleted) this.appDataManager.onDeleteBody(id);
        this.canvasView.addFrameData("deleted", deleted);

        // Handle updates from the physics engine
        this.canvasView.addFrameData("updated", updated);

        // Handle updates from the metadata queue
        const metaDataUpdated = this.appDataManager.handleQueuedUpdates();
        this.canvasView.addFrameData("updated", metaDataUpdated);
    }

    //#endregion


    //#region Pausing

    static #paused = true;

    /**
     * Gets the current paused state of the simulation.
     * @returns {boolean} `true` if the simulation is paused, `false` otherwise.
     */
    static get paused() { return this.#paused; } 

    /**
     * Toggles or sets the paused state of the simulation.
     * @param {boolean} [force]     If provided, sets the paused state directly (`true` for paused, `false` for running).
     * @returns {boolean} The new paused state.
     */
    static togglePause(force=undefined) {
        this.#paused = force === undefined
            ? !this.#paused
            : !!force;
        return this.#paused;
    }

    //#endregion


    //#region Presets

    /**
     * Gathers preset data from all relevant components and serializes it into a single JSON string.
     * @returns {string} A JSON string representing the complete state of the simulation.
     */
    static getPreset() {
        const data = {
            bodyData: this.appDataManager.getPresetData(),
            simDataStr: this.Bridge.getPreset()
        };
        return JSON.stringify(data);
    }

    /**
     * Loads a simulation state from a JSON preset string.
     * This is a destructive operation that replaces the current simulation state.
     * @param {string} presetString             The JSON string representing the desired state.
     * @param {boolean} [preserveState=true]    If `true`, the current state is saved before loading. If loading fails,
     *                                          the saved state is restored. 
     *                                          If `false`, a failure will throw an unrecoverable error.
     * @returns {void}
     */
    static loadPreset(presetString, preserveState=true) {
        const prevState = preserveState ? this.getPreset() : null;

        try {
            const {bodyData, simDataStr} = JSON.parse(presetString);
            this.appDataManager.loadPresetData(bodyData);
            this.Bridge.loadPreset(simDataStr); // throws if JSON is invalid
        } catch (err) {
            this.notifications.add(`Invalid Preset`);
            if(prevState) {
                console.error(err.message, err);
                return this.loadPreset(prevState, false);
            }
            else throw new Error(`Invalid Preset Error`, {cause: err});
        }
    }

    //#endregion


    //#region Utility

    /**
     * A centralized error handling function. This can be passed as a callback
     * to other components to standardize error reporting.
     * @param {Error} err                   The error object.
     * @param {object} [config]             Additional configuration for handling the error.
     * @param {string} [config.notifMsg]    A user-friendly message to display as a notification.
     * @param {boolean} [config.isFatal]    If `true`, the error will be re-thrown, halting execution.
     */
    static onError(err, {notifMsg = "", isFatal=false}={}) {
        console.error(err);
        if(notifMsg) this.notifications.add(notifMsg);
        if(isFatal) throw err;
    }

    /**
     * While debug mode is enabled, logs messages and optional data to the console.
     * @param {string} msg  The message to log.
     * @param {any} [data]  Optional data to log alongside the message.
     * @returns {void}
     */
    static log(msg, data=undefined) {
        if(this.#CONFIG.debugMode) {
            if(data) console.log(msg, data);
            else console.log(msg);
        }
    }

    //#endregion
}

// Helper for quickly setting up scenarios during development.
// TODO: Make into separate file and don't include in release builds
class Scenarios {
    async FourBodySystemSymmetrical() {
        const id1 = await AppShell.createBody();
        const id2 = await AppShell.createBody();
        const id3 = await AppShell.createBody();
        const id4 = await AppShell.createBody();

        await AppShell.updateBody(id1, {enabled: 1, mass: 1, posX: 1, posY: 1, velY: -1, name: "1"});
        await AppShell.updateBody(id2, {enabled: 1, mass: 1, posX: 1, posY: -1, velX: -1, name: "2"});
        await AppShell.updateBody(id3, {enabled: 1, mass: 1, posX: -1, posY: -1, velY: 1, name: "3"});
        await AppShell.updateBody(id4, {enabled: 1, mass: 1, posX: -1, posY: 1, velX: 1, name: "4"});
    }
}
