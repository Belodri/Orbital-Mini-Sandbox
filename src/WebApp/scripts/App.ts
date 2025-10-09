import Bridge, { BodyId } from "@bridge";
import AppData from "./AppData";
import Notifications from "./UI/Notifications";
import UiData from "./UI/UiData";
import PixiHandler from "./UI/PixiHandler";
import UiManager from "./UI/UiManager";
import DeferredResolver from "./utils/DeferredResolver";
import { log } from "./utils/Logger";

// TODO: Add preset methods

class App {
    static #instanceField: App;
    static get #instance() {
        if(!App.#instanceField) throw new Error("App has not been initialized.");
        return App.#instanceField;
    }

    //#region Public API

    /**
     * Toggles or sets the paused state of the simulation.
     * @param force     If provided, sets the paused state directly (`true` for paused, `false` for running). If omitted, the state is toggled.
     */
    static togglePause(force?: boolean): void { 
        this.#instance.#paused = force === undefined ? !this.#instance.#paused : force; 
    }

    /**
     * Creates a new body in the simulation.  
     * Code is executed immediately and can throw synchronous errors!
     * @returns         A promise that resolves with the unique ID of the new body at the start of the next post-render phase.
     */
    static async createBody(): Promise<BodyId> { return this.#instance.#resolver.execute(Bridge.createBody); }

    /**
     * Deletes a body from the simulation.  
     * Code is executed immediately and can throw synchronous errors!
     * @param id        The unique ID of the body to delete.
     * @returns         A promise that resolves resolves at the start of the next post-render phase. `true` if the body was deleted, or `false` if it wasn't found.
     */
    static async deleteBody(id: BodyId): Promise<boolean> { return this.#instance.#resolver.execute(() => Bridge.deleteBody(id)); }

    /**
     * Updates the simulation's app data or physics data.  
     * Code is executed immediately and can throw synchronous errors!
     * @param updates   Partial update data.
     * @returns         A promise that resolves at the start of the next post-render phase.
     */
    static async updateSimulation(updates: { physics: Parameters<typeof Bridge["updateSimulation"]>[0] }): Promise<void>;
    static async updateSimulation(updates: { app: Parameters<AppData["updateSimulationData"]>[0] }): Promise<void>;
    static async updateSimulation(updates: { app: Parameters<AppData["updateSimulationData"]>[0] } | { physics: Parameters<typeof Bridge["updateSimulation"]>[0] }): Promise<void> {
        if("app" in updates && "physics" in updates) throw new Error("App | updateSimulation cannot update app and physics data simultaneously."); 
        return this.#instance.#resolver.execute(() => "app" in updates ? this.#instance.#appData.updateSimulationData(updates.app) : Bridge.updateSimulation(updates.physics));
    }

    /**
     * Updates a specific body's app data or physics data.
     * This code is executed immediately and can throw synchronous errors!
     * @param id The unique ID of the body to update.
     * @param updates An object containing either `app` or `physics` data for the update.
     * @returns A promise that resolves with a boolean indicating if the update was successful (e.g., if the body exists).
     */
    static async updateBody(id: BodyId, updates: { physics: Parameters<typeof Bridge["updateBody"]>[1] }): Promise<boolean>;
    static async updateBody(id: BodyId, updates: { app: Parameters<AppData["updateBodyData"]>[1] }): Promise<boolean>;
    static async updateBody(id: BodyId, updates: { app: Parameters<AppData["updateBodyData"]>[1] } | { physics: Parameters<typeof Bridge["updateBody"]>[1] }): Promise<boolean> {
        if("app" in updates && "physics" in updates) throw new Error("App | updateBody cannot update app and physics data simultaneously."); 
        return this.#instance.#resolver.execute(() => "app" in updates ? this.#instance.#appData.updateBodyData(id, updates.app) : Bridge.updateBody(id, updates.physics));
    }

    /** 
     * Initializes the entire application. Calling any other method before this one will throw an Error.
     * Repeated initialization calls are safely ignored.
     */
    static async init(): Promise<void> {
        if(App.#instanceField) return;

        log("Begin initialization.");

        const app = new App();

        await Bridge.initialize(__DEBUG__);

        await PixiHandler.init({
            preRender: app.#preRender,
            render: app.#render,
            postRender: app.#postRender
        }, { autoStart: !app.#paused }, __DEBUG__);

        UiData.init({
            physicsState: Bridge.state,
            physicsDiff: Bridge.diff,
            appState: app.#appData.state,
            appDiff: app.#appData.diff
        });

        Notifications.init();
        UiManager.init();

        App.#instanceField = app;

        if(__DEBUG__) {
            // @ts-ignore
            globalThis.App = App;

            Object.defineProperties(App, {
                "Bridge": { value: Bridge },
                "PixiHandler": { value: PixiHandler },
                "UiData": { value: UiData },
                "Notifications": { value: Notifications },
                "appData": { get() { return App.#instance.#appData }},
                "resolver": { get() { return App.#instance.#resolver }},
            });
        }

        App.#instance.#startRenderLoop();
        log("Initialization complete.");
    }

    //#endregion

    #resolver: DeferredResolver = new DeferredResolver();
    #appData: AppData = new AppData();
    #paused: boolean = false;
    #started: boolean = false;

    //#region Render Loop

    #renderLogCount = 0;
    get #logNextRender() { return this.#renderLogCount > 0; };
    
    static logNextRenders(count: number) {
        if(count <= 0) return;
        this.#instance.#renderLogCount = count;
    }

    /** 
     * Orchestrates physics calculations and data transformations.  
     * Called by PixiHandler at the very beginning of a new render frame. 
     */
    #preRender = () => {
        if(this.#logNextRender) {
            this.#renderLogCount--;
            log("preRender");
        }

        try {
            const syncOnly = this.#paused;
            Bridge.tickEngine(syncOnly);

            const { created, deleted } = Bridge.diff.bodies;
            this.#appData.syncDiff(created, deleted);

            UiData.refresh();
        } catch(err) {
            this.#onFatalError(err, `Fatal Pre-Render Error.`);
        }
    }

    /** 
     * Orchestrates rendering.  
     * Called by PixiHandler after preRender tasks are completed. 
     */
    #render = () => {
        if(this.#logNextRender) log("render");
        try {
            UiManager.render(this.#paused);
        } catch(err) {
            this.#onFatalError(err, `Fatal Render Error.`);
        }
    }

    /**
     * Orchestrates resolving of deferred API promises.  
     * Called by PixiHandler after rendering is fully complete. 
     */
    #postRender = () => {
        if(this.#logNextRender) log("postRender");
        try {
            this.#resolver.resolve();

            if(this.#logNextRender) log("postRender: End");
        } catch(err) {
            this.#onFatalError(err, `Fatal Post-Render Error.`);
        }
    }

    //#endregion

    /** Starts the Pixi-controlled rendering loop. */
    #startRenderLoop = () => {
        if(this.#started) return;
        PixiHandler.start();
        this.#started = true;
    }

    /** Stops the Pixi-controlled rendering loop. */
    #stopRenderLoop = () => {
        if(!this.#started) return;
        PixiHandler.stop();
        this.#started = false;
    }

    #onFatalError(err: unknown, notif: string) {
        this.#stopRenderLoop();
        console.error(err);
        Notifications.error(notif);
        // TODO: Option to reset to last snapshot
    }
}

namespace App {
    export let Bridge: typeof import("@bridge").default
    export let PixiHandler: typeof import("./UI/PixiHandler").default;
    export let UiData: typeof import("./UI/UiData").default;
    export let Notifications: typeof import("./UI/Notifications").default;
    export let appData: AppData;
    export let resolver: DeferredResolver;
}

export default App;
