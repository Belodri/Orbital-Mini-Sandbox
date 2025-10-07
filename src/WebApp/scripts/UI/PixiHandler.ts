import { Application, ApplicationOptions, Container, ContainerChild, CullerPlugin, extensions, Graphics, Renderer, Sprite, Texture, UPDATE_PRIORITY } from "pixi.js";
import { initDevtools } from "@pixi/devtools";
import { BodyView } from "./UiData";
import { BodyId } from "@bridge";

const PIXI_APP_OPTIONS_DEFAULTS: Partial<ApplicationOptions>  = {
    autoStart: false,
    resizeTo: window,
    backgroundColor: "black",
    preference: "webgpu",
} as const;

const BODY_TOKEN_CONFIG = {
    disabledAlpha: 0.5,
    textureRadius: 5
} as const;

const PIXI_HANDLER_CONFIG = {
    ZOOM_MIN: 0.01,
    ZOOM_MAX: 100,
    ZOOM_FACTOR: 0.05,
    PIXELS_PER_AU: 100,
    MIN_FPS: 30,
    MAX_FPS: 60
} as const;

export type PixiHandlerDependencies = {
    preRender: () => void;
    render: () => void;
    postRender: () => void;
}

class BodyToken {
    static #texture: Texture;

    /** Must be called at least once before an instance can be created! */
    static init(renderer: Renderer): void {
        const circle = new Graphics()
            .circle(0, 0, BODY_TOKEN_CONFIG.textureRadius)
            .fill("white");
        BodyToken.#texture = renderer.generateTexture(circle);
    }

    #view: BodyView;
    readonly sprite: Sprite = new Sprite(BodyToken.#texture);

    get app() { return this.#view.app; }
    get physics() { return this.#view.physics; }

    constructor(view: BodyView) {
        this.#view = view;
    }

    updateSprite(sceneScale: number) {
        this.sprite.position.set(
            this.physics.posX * PIXI_HANDLER_CONFIG.PIXELS_PER_AU,
            this.physics.posY * PIXI_HANDLER_CONFIG.PIXELS_PER_AU
        );

        this.sprite.tint = this.app.tint;
        this.sprite.alpha = this.physics.enabled ? 1 : BODY_TOKEN_CONFIG.disabledAlpha;

        this.sprite.scale.set(1 / sceneScale);
    }
}

/** 
 * Static facade for all interactions with Pixi.
 * 
 * Dumb rendering component, that notifies controlling code via injected callbacks.
 */
export default class PixiHandler {
    static #instanceField: PixiHandler;

    static get #instance() { 
        if(!PixiHandler.#instanceField) throw new Error("PixiHandler has not been initialized.");
        return PixiHandler.#instanceField;
    }

    //#region Public API

    /** Gets the PixiHandler's CameraControls component. */
    static get camera(): CameraControls { return this.#instance.#cameraControls; }

    /**
     * Initializes the class. Calling any other method before this one will throw an Error.
     * Repeated initialization calls are safely ignored.
     * @param dependencies        
     * @param pixiAppOptions    Partial application options for the pixi {@link Application}. Merges with and overrides default options.
     * @param enableDevTools    Enable PIXI's dev tools? Default = `false`
     */
    static async init(dependencies: PixiHandlerDependencies, pixiAppOptions: Partial<ApplicationOptions> = {}, enableDevTools: boolean = false) : Promise<void> {
        if(PixiHandler.#instanceField) return;

        const app = new Application();
        await app.init({
            ...PIXI_APP_OPTIONS_DEFAULTS,
            ...pixiAppOptions
        });
        extensions.add(CullerPlugin);
        BodyToken.init(app.renderer);

        PixiHandler.#instanceField = new PixiHandler(app, dependencies);
        if(enableDevTools) initDevtools({ app: PixiHandler.#instance.#app });
    }

    /**
     * Adds a newly created body to pixi rendering. Assumes data has been processed and verified by {@link UiData} already!
     * @param view  The BodyView of the newly created body.
     */
    static onCreateBody(view: BodyView): void { PixiHandler.#instance.#onCreateBody(view); }

    /**
     * Removes a deleted body from pixi rendering. Assumes data has been processed and verified by {@link UiData} already!
     * @param id    The ID of the deleted body.
     */
    static onDeleteBody(id: number): void { PixiHandler.#instance.#onDeleteBody(id); }

    /**
     * Updates a rendered body. Assumes data has been processed and verified by {@link UiData} already!
     * @param {number} id   The ID of the updated body.
     */
    static onUpdateBody(id: number): void { PixiHandler.#instance.#onUpdateBody(id); }

    /** Starts the render/update loop. */
    static start() { PixiHandler.#instance.#app.start(); }
    
    /** Stops the render/update loop. */
    static stop() { PixiHandler.#instance.#app.stop(); }

    //#endregion

    readonly #app: Application;
    readonly #scene: Container<ContainerChild>;
    readonly #dependencies: PixiHandlerDependencies;
    readonly #bodyTokens: Map<BodyId, BodyToken> = new Map();
    readonly #cameraControls: CameraControls;

    private constructor(app: Application, injections: PixiHandlerDependencies) {
        this.#app = app;
        this.#dependencies = injections;

        document.body.appendChild(this.#app.canvas);
        this.#scene = new Container({
            cullable: true,
            cullableChildren: true,
            isRenderGroup: true
        });
        this.#app.stage.addChild(this.#scene);

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
            this.#dependencies.postRender();
        }

        // Register event listeners
        this.#cameraControls = new CameraControls(this.#app, this.#scene);

        // Initialize the render loop using the PIXI ticker.
        /*
            This render loop runs constantly, even while the simulation is paused,
            to enable continuous camera controls and UI responsiveness. It is split
            into two phases using PIXI's update priority system:
            HIGH calls `preRender` callback to signal that calculations can be performed.
            NORMAL calls `render` callback to signal that rendering can be performed.
        */
        this.#app.ticker.add(this.#dependencies.preRender, this, UPDATE_PRIORITY.HIGH);
        this.#app.ticker.add(this.#dependencies.render, this, UPDATE_PRIORITY.NORMAL);
        this.#app.ticker.maxFPS = PIXI_HANDLER_CONFIG.MAX_FPS;
        this.#app.ticker.minFPS = PIXI_HANDLER_CONFIG.MIN_FPS;
        if(!this.#app.ticker.started) this.#app.ticker.start();
    }

    #onCreateBody(view: BodyView): void {
        const bodyToken = new BodyToken(view);
        this.#bodyTokens.set(view.id, bodyToken);
        this.#scene.addChild(bodyToken.sprite);
        bodyToken.updateSprite(this.#scene.scale.x);
    }

    #onDeleteBody(id: BodyId): void {
        const token = this.#bodyTokens.get(id);
        if(token) {
            this.#scene.removeChild(token.sprite);
            this.#bodyTokens.delete(id);
        }
    }

    #onUpdateBody(id: BodyId): void {
        const token = this.#bodyTokens.get(id);
        if(token) token.updateSprite(this.#scene.scale.x);
    }
}

/**
 * Purpose-built component for the {@link PixiHandler} which handles panning and zooming the camera.
 * Registers event listeners to the `canvas` of the injected {@link Application}. 
 * 
 * Assumes that the `Application` has been properly initialized beforehand!
 */
class CameraControls {
    #app: Application;
    #scene: Container<ContainerChild>;

    #isDragging = false;
    #dragPointerPos = { x: 0, y: 0 };

    constructor(app: Application, scene: Container<ContainerChild>) {
        this.#app = app;
        this.#scene = scene;

        const canvas = this.#app.canvas;
        canvas.addEventListener("pointerdown", this.#onPointerDown);
        canvas.addEventListener("pointerup", this.#onPointerUp);
        canvas.addEventListener("pointermove", this.#onPointerMove);
        canvas.addEventListener("pointerout", this.#onPointerOut);
        canvas.addEventListener("wheel", this.#onWheel, { passive: true });
    }

    /**
     * Centers the view on a specific point.
     * @param x             The x-coordinate of the point to pan to.
     * @param y             The y-coordinate of the point to pan to.
     * @param isSimCoord    If `true`, `x` and `y` are interpreted as simulation coordinates (AU).
     *                      If `false`, they are interpreted as screen coordinates (pixels).
     */
    panToPoint(x: number, y: number, isSimCoord: boolean = true): void {
        const screenCenterX = this.#app.screen.width / 2;
        const screenCenterY = this.#app.screen.height / 2;

        let targetX: number, targetY : number;

        if(isSimCoord) {
            // Find where this simulation point exists in the scaled PIXI world.
            const worldX = x * this.#scene.scale.x;
            const worldY = y * this.#scene.scale.y;

            // To center this point, the scene's top-left corner must be offset
            // from the screen's center by that point's world coordinates.
            targetX = screenCenterX - worldX;
            targetY = screenCenterY - worldY;
        } else {
            // Pan the scene to move the given screen point (x, y) to the center of the viewport.
            const deltaX = screenCenterX - x;
            const deltaY = screenCenterY - y;
            targetX = this.#scene.x + deltaX;
            targetY = this.#scene.y + deltaY;
        }

        this.#scene.position.set(targetX, targetY);
    }

    /**
     * Pans the scene by a given pixel delta.
     * @param dx    The change in x-position in pixels.
     * @param dy    The change in y-position in pixels.
     */
    panDelta(dx: number, dy: number) {
        const {x, y} = this.#scene;
        this.#scene.position.set(x + dx, y + dy);
    }

    #onPointerDown = (e: PointerEvent) => {
        if(e.button === 0) {
            this.#isDragging = true;
            this.#dragPointerPos.x = e.clientX;
            this.#dragPointerPos.y = e.clientY;
        }
    }

    #onPointerUp = (e: PointerEvent) => {
        if(e.button === 0) this.#isDragging = false;
    }

    #onPointerMove = (e: PointerEvent) => {
        if(this.#isDragging) {
            const deltaX = e.clientX - this.#dragPointerPos.x;
            const deltaY = e.clientY - this.#dragPointerPos.y;
            this.panDelta(deltaX, deltaY);

            this.#dragPointerPos.x = e.clientX;
            this.#dragPointerPos.y = e.clientY;
        }
    }

    #onPointerOut = () => {
        this.#isDragging = false;
    }

    /** Handles the 'wheel' event to zoom the scene in or out, centered on the mouse pointer. */
    #onWheel = (e: WheelEvent) => {
        const {ZOOM_MAX, ZOOM_MIN, ZOOM_FACTOR} = PIXI_HANDLER_CONFIG;

        const zoomDirection = e.deltaY > 0 ? -1 : 1;
        const newScale = this.#scene.scale.x + (zoomDirection * ZOOM_FACTOR);
        if(newScale < ZOOM_MIN || newScale > ZOOM_MAX) return;

        const mousePos = {x: e.offsetX, y: e.offsetY};
        const scenePosPreZoom = this.#scene.toLocal(mousePos);

        this.#scene.scale.set(newScale);

        const scenePosPostZoom = this.#scene.toLocal(mousePos);

        this.#scene.x -= (scenePosPostZoom.x - scenePosPreZoom.x) * newScale;
        this.#scene.y -= (scenePosPostZoom.y - scenePosPreZoom.y) * newScale;
    }
}