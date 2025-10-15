import Bridge, { IBridge } from "@bridge";
import AppData, { IAppData } from "./Data/AppData";
import DataViews, { IDataViews } from "./Data/DataViews";
import Notifications, { INotifications } from "./UI/Notifications/Notifications";
import PixiHandler, { IPixiHandler } from "./UI/PixiHandler/PixiHandler";
import UiManager, { IUiManager } from "./UI/UiManager";
import DeferredResolver, { IDeferredResolver } from "./utils/DeferredResolver";
import Controller, { IController } from "./Controller/Controller";
import RenderLoop, { IRenderLoop } from "./Controller/RenderLoop";
import { log } from "./utils/Logger";

export type Parts =  {
    bridge: IBridge;
    appData: IAppData;
    views: IDataViews;
    notif: INotifications;
    pixi: IPixiHandler;
    uiManager: IUiManager;
    resolver: IDeferredResolver;
}

export interface IApp extends 
    Pick<IController, "createBody" | "deleteBody" | "updateBody" | "updateSimulation">,
    Pick<IRenderLoop, "togglePause" | "start" | "stop">
{};

export interface IAppDebug extends IApp {
    parts: Parts;
    controller: IController;
    renderLoop: IRenderLoop;
}

declare global {
    interface Window { App: IAppDebug; }  // only on debug build
}

export default class App implements IApp {
    static #instance: App;

    static async init(): Promise<void> {
        if(App.#instance) throw new Error("App.init() should only be called once.");

        log("Begin initialization...");

        // Data
        log("Initialize AppData...");
        const appData = new AppData();
        log("Initialize Views...");
        const views = new DataViews();

        // WASM Interop
        log("Initialize Bridge...");
        const bridge = await Bridge.init();

        // Utilities
        log("Initialize Notifications...");
        const notif = new Notifications();
        log("Initialize Resolver...");
        const resolver = new DeferredResolver();

        // UI
        log("Initialize PixiHandler...");
        const pixi = await PixiHandler.init();
        log("Initialize UiManager...");
        const uiManager = new UiManager(pixi, notif);

        const parts = { bridge, appData, views, notif, resolver, pixi, uiManager };

        // Orchestrators
        log("Initialize RenderLoop...");
        const renderLoop = new RenderLoop(parts);
        log("Initialize Controller...");
        const controller = new Controller(parts);
        
        // Wiring
        log("Register Pixi Events...");
        parts.pixi.events.registerEventListener("preRender", renderLoop.preRender.bind(renderLoop));
        parts.pixi.events.registerEventListener("render", renderLoop.render.bind(renderLoop));
        parts.pixi.events.registerEventListener("postRender", renderLoop.postRender.bind(renderLoop));

        log("Create App Instance...");
        App.#instance = new App(parts, controller, renderLoop);

        log("Initialization complete!");
    }

    #parts: Parts;
    #controller: IController;
    #renderLoop: IRenderLoop;

    private constructor(parts: Parts, controller: IController, renderLoop: IRenderLoop) {
        this.#parts = parts;
        this.#controller = controller;
        this.#renderLoop = renderLoop;

        if(__DEBUG__) {
            Object.defineProperties(this, {
                "parts": { get() { return this.#parts; }, enumerable: true },
                "controller": { get() { return this.#controller }, enumerable: true},
                "renderLoop": { get() { return this.#renderLoop }, enumerable: true}
            });

            globalThis.window.App = this as unknown as IAppDebug;
        }
    }

    get createBody() { return this.#controller.createBody; }
    get deleteBody() { return this.#controller.deleteBody; }
    get updateBody() { return this.#controller.updateBody; }
    get updateSimulation() { return this.#controller.updateSimulation; }
    get togglePause() { return this.#renderLoop.togglePause; }
    get start() { return this.#renderLoop.start; }
    get stop() { return this.#renderLoop.stop; }
}
