import { Application, ApplicationOptions, Container, ContainerChild, CullerPlugin, extensions, Renderer, UPDATE_PRIORITY } from "pixi.js";
import { initDevtools } from "@pixi/devtools";
import { IDataViews, BodyView } from "../../Data/DataViews";
import { BodyId } from "@bridge";
import { BodyToken } from "./BodyToken";
import { CameraControls } from "./CameraControls";
import EventHandler, { IEventHandler } from "@webapp/scripts/utils/EventHandler";

const PIXI_APP_OPTIONS_DEFAULTS: Partial<ApplicationOptions>  = {
    autoStart: false,
    resizeTo: window,
    backgroundColor: "black",
    preference: "webgpu",
    sharedTicker: true,
} as const;

export const PIXI_HANDLER_CONFIG = {
    ZOOM_MIN: 0.01,
    ZOOM_MAX: 100,
    ZOOM_FACTOR: 0.05,
    PIXELS_PER_AU: 100,
    MIN_FPS: 30,
    MAX_FPS: 60
} as const;

type HandlerEvent = "preRender" | "render" | "postRender";

/** 
 * Facade for all interactions with Pixi.
 * Dumb rendering component, that notifies controlling code via events.
 */
export interface IPixiHandler {
    /** Event handler for managing callbacks. Register via `registerCallback()`. */
    events: IEventHandler<HandlerEvent>;
    /**
     * Adds a newly created body to pixi rendering. Assumes data has been processed and verified by {@link IDataViews} already!
     * @param view  The BodyView of the newly created body.
     */
    onCreateBody(view: BodyView): void;
    /**
     * Removes a deleted body from pixi rendering. Assumes data has been processed and verified by {@link IDataViews} already!
     * @param id    The ID of the deleted body.
     */
    onDeleteBody(id: number): void;
    /**
     * Updates a rendered body. Assumes data has been processed and verified by {@link IDataViews} already!
     * @param {number} id   The ID of the updated body.
     */
    onUpdateBody(id: number): void;
    /** Starts the render/update loop. */
    start(): void;
    /** Stops the render/update loop. */
    stop(): void;
    /**
     * Centers the view on a specific point.
     * @param x             The x coordinate of the point to pan to.
     * @param y             The y coordinate of the point to pan to.
     * @param physicsPoint  (default = true) Is point in physics coordinates, rather than screen coordinates (pixels)?
     */
    panToPoint(x: number, y: number, physicsPoint?: boolean): void;
}

export default class PixiHandler implements IPixiHandler {
    static #instance: PixiHandler;

    readonly #events: IEventHandler<HandlerEvent> = new EventHandler<HandlerEvent>();
    readonly #app: Application;
    readonly #scene: Container<ContainerChild>;
    readonly #bodyTokens: Map<BodyId, BodyToken> = new Map();
    readonly #cameraControls: CameraControls;

    //#region Initialization

    /** Initializes the class. Idempotent. */
    static async init(): Promise<PixiHandler> {
        if(PixiHandler.#instance) return PixiHandler.#instance;

        const app = new Application();
        await app.init(PIXI_APP_OPTIONS_DEFAULTS);

        // Shared ticker must be stopped manually.
        if(PIXI_APP_OPTIONS_DEFAULTS.sharedTicker && !PIXI_APP_OPTIONS_DEFAULTS.autoStart) app.ticker.stop();

        extensions.add(CullerPlugin);
        BodyToken.init(app.renderer);

        PixiHandler.#instance = new PixiHandler(app);
        if(__DEBUG__) initDevtools({ app: PixiHandler.#instance.#app });

        return PixiHandler.#instance;
    }

    private constructor(app: Application) {
        this.#app = app;

        this.#scene = new Container({
            cullable: true,
            cullableChildren: true,
            isRenderGroup: true
        });
        this.#app.stage.addChild(this.#scene);
        this.#cameraControls = new CameraControls(this.#app, this.#scene);

        // Monkeypatch Pixi Renderer
        /*
            Wraps the `Application.renderer.render()` method to detect the end 
            of the rendering process and call the `postRender` callback.
            This must be done AFTER the promise from `Application.init()` has resolved!

            Pixi's `EventRunner` could be used to listen to its internal `postrender` event but
            that would massively increase complexity over a monkeypatch for little to no gain.
            The `postrender` event is otherwise entirely inaccessible in PIXI v8.x.
        */
        const initialRendererFunc = this.#app.renderer.render;
        this.#app.renderer.render = (...any: any[]) => {    // We're just passing the arguments through so this is fine.
            initialRendererFunc.call(this.#app.renderer, ...any as Parameters<Renderer["render"]>);
            this.#events.callEventListeners("postRender");
        }

        // Initialize the render loop using the PIXI ticker.
        /*
            This render loop runs constantly, even while the simulation is paused,
            to enable continuous camera controls and UI responsiveness. It is split
            into two phases using PIXI's update priority system:
            HIGH calls `preRender` callback to signal that calculations can be performed.
            NORMAL calls `render` callback to signal that rendering can be performed.
        */
        this.#app.ticker.add(() => this.#events.callEventListeners("preRender"), this, UPDATE_PRIORITY.HIGH);
        this.#app.ticker.add(() => this.#events.callEventListeners("render"), this, UPDATE_PRIORITY.NORMAL);
        this.#app.ticker.maxFPS = PIXI_HANDLER_CONFIG.MAX_FPS;
        this.#app.ticker.minFPS = PIXI_HANDLER_CONFIG.MIN_FPS;

        document.body.appendChild(this.#app.canvas);
    }


    //#endregion

    get events() { return this.#events; }

    onCreateBody(view: BodyView): void {
        const bodyToken = new BodyToken(view);
        this.#bodyTokens.set(view.id, bodyToken);
        this.#scene.addChild(bodyToken.sprite);
        bodyToken.updateSprite(this.#scene.scale.x);
    }

    onDeleteBody(id: BodyId): void {
        const token = this.#bodyTokens.get(id);
        if(token) {
            this.#scene.removeChild(token.sprite);
            this.#bodyTokens.delete(id);
        }
    }

    onUpdateBody(id: BodyId): void {
        const token = this.#bodyTokens.get(id);
        if(token) token.updateSprite(this.#scene.scale.x);
    }

    start(): void { this.#app.start(); }

    stop(): void { this.#app.stop(); }

    panToPoint(x: number, y: number, physicsPoint: boolean = true): void {
        this.#cameraControls.panToPoint(x, y, physicsPoint);
    }
}
