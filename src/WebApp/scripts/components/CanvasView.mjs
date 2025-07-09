import { Application, Container, CullerPlugin, extensions, Graphics, Sprite, Texture, UPDATE_PRIORITY } from "pixi.js";
import { initDevtools } from "@pixi/devtools";

/**
 * @import { BodyStateData } from  "../../types/Bridge"
 * @import { BodyMetaData } from "../AppDataManager.mjs"
 */


/**
 * Manages the PIXI.js canvas, rendering, and user interactions like panning and zooming.
 * 
 * @singleton The elements and listeners managed by an instance of this class are not destroyed and could cause memory leaks otherwise!
 */
export default class CanvasView {
    //#region Initialization

    /**
     * Configuration options for the CanvasView's behavior.
     * @typedef {object} CanvasViewConfig
     * @property {number} ZOOM_MIN          The minimum zoom level (scene scale).
     * @property {number} ZOOM_MAX          The maximum zoom level (scene scale).
     * @property {number} ZOOM_FACTOR       The amount to change the zoom level on each wheel event.
     * @property {number} PIXELS_PER_AU     The scale factor for converting simulation coordinates (in AU) to screen pixels.
     * @property {boolean} DEBUG            Whether to enable debug features like the PIXI Devtools.
     */

    /**
     * An object containing all external dependencies required by the CanvasView.
     * @typedef {object} Injections
     * @property {Readonly<Map<number, BodyStateData>>} bodyStateDataStore                          A read-only map of body physics states.
     * @property {Readonly<Map<number, BodyMetaData>>} bodyMetaDataStore                            A read-only map of body metadata.
     * @property {(err: Error, config?: {notifMsg?: string, isFatal?: boolean}) => void} onError    A centralized error handling function.
     */

    /**
     * The factory method for creating and initializing the CanvasView singleton.
     * @param {Injections} injections                                               The required external dependencies.
     * @param {Partial<import("pixi.js").ApplicationOptions>} [pixiInitArgs={}]     Custom arguments to pass to the PIXI.Application constructor.
     * @param {Partial<CanvasViewConfig>} [canvasViewConfig={}]                     Custom configuration for the CanvasView instance.
     * @returns {Promise<CanvasView>} A promise that resolves with the created CanvasView instance.
     */
    static async create(injections, pixiInitArgs = {}, canvasViewConfig={}) {
        if(CanvasView.#instance) throw new Error("CanvasView has already been created.");

        const app = new Application();
        const appArgs = {
            autoStart: true,
            resizeTo: window,
            backgroundColor: "black",
            preference: /** @type {'webgpu'} */ ('webgpu'),
            powerPreference: /** @type {'high-performance'} */ ('high-performance'),
            ...pixiInitArgs
        };

        await app.init(appArgs);
        extensions.add(CullerPlugin);

        CanvasView.#instance = new CanvasView(app, injections, canvasViewConfig);
        return CanvasView.#instance;
    }

    /**
     * The private constructor for CanvasView. Use the static `create` method for instantiation.
     * @param {Application} app                         The initialized PIXI.Application instance.
     * @param {Injections} injections                   The object containing required dependencies.
     * @param {Partial<CanvasViewConfig>} [config={}]   Configuration overrides.
     * @private
     */
    constructor(app, injections, config={}) {
        this.#CONFIG = {
            ...this.#CONFIG,
            ...config
        };

        this.#bodyStateDataStore = injections.bodyStateDataStore;
        this.#bodyMetaDataStore = injections.bodyMetaDataStore;
        this.#onError = injections.onError;

        this.#app = app;
        document.body.appendChild(this.#app.canvas);
        BodyToken.staticInitBodyToken(this.#app.renderer)
        this.#initScene();
        this.#registerEventListeners();
        this.#initRenderLoop();

        if(this.#CONFIG.DEBUG) {
            initDevtools({ app: this.#app });
        }
    }

    /**
     * Initializes the main PIXI.Container that will hold all simulation objects.
     */
    #initScene() {
        const scene = new Container({
            cullable: true,
            cullableChildren: true,
            isRenderGroup: true,
        });

        this.#scene = scene;
        this.#app.stage.addChild(this.#scene);
    }

    /**
     * Registers pointer and wheel event listeners on the canvas for camera controls.
     */
    #registerEventListeners() {
        this.#app.canvas.addEventListener("pointerdown", this.#onPointerDown.bind(this));
        this.#app.canvas.addEventListener("pointerup", this.#onPointerUp.bind(this));
        this.#app.canvas.addEventListener("pointermove", this.#onPointerMove.bind(this));
        this.#app.canvas.addEventListener("pointerout", this.#onPointerOut.bind(this));
        this.#app.canvas.addEventListener("wheel", this.#onWheel.bind(this), { passive: true });
    }

    /** 
     * Initializes the render loop using the PIXI ticker.
     * This render loop runs constantly, even while the simulation is paused,
     * to enable continuous camera controls and UI responsiveness. It is split
     * into two phases using PIXI's update priority system:
     * 1. HIGH priority: Emits 'renderFrameReady' to signal that the physics engine can perform its tick.
     * 2. NORMAL priority: Processes the rendering queue (creates, deletes, updates sprites).
     */
    #initRenderLoop() {
        this.#app.ticker.add((ticker) => {
            this.#callCallback("renderFrameReady", ticker.deltaTime);
        }, this, UPDATE_PRIORITY.HIGH);

        this.#app.ticker.add(() => {
            this.#handleRender();
            this.#resolveNextFramePromises();
        }, this, UPDATE_PRIORITY.NORMAL);
    }

    //#endregion

    /** @type {CanvasView} Singleton instance of the CanvasView */
    static #instance;

    /** @type {CanvasViewConfig} Configuration for the CanvasView instance with default values.*/
    #CONFIG = {
        ZOOM_MIN: 0.01,
        ZOOM_MAX: 10000,
        ZOOM_FACTOR: 0.05,
        /** @type {number} How many pixels is 1 AU? */
        PIXELS_PER_AU: 100,
        DEBUG: true,
    }

    /** @type {Application} The root PIXI.Application instance. */
    #app;

    /** 
     * The main PIXI.Container that holds all rendered objects.
     * Panning and zooming are achieved by transforming this container.
     * @type {Container} 
     */
    #scene;

    /** @type {Map<number, BodyToken>} A map linking body IDs to their visual `BodyToken` instances. */
    #bodyTokens = new Map();


    //#region Injections

    /** @type {Readonly<Map<number, BodyStateData>>} A read-only map of body physics states, injected on creation. */
    #bodyStateDataStore;

    /** @type {Readonly<Map<number, BodyMetaData>>} A read-only map of body metadata, injected on creation. */
    #bodyMetaDataStore;

    /** @type {(err: Error, config?: {notifMsg?: string, isFatal?: boolean}) => void} A centralized error handler, injected on creation. */
    #onError;

    //#endregion


    //#region Callbacks

    /** @type {Map<string, Function>} */
    #callbacks = new Map();

    /**
     * Registers a callback function for a specific event, allowing this view
     * to communicate with external systems (like AppShell) without a hard dependency.
     * @param {"renderFrameReady"} event    The name of the event to listen for.
     * @param {Function} fn                 The callback function to execute when the event is fired.
     */
    registerCallback(event, fn) {
        if(typeof fn !== "function") throw new TypeError(`'fn' argument must be typeof "function".`);
        if(this.#callbacks.has(event)) throw new Error(`A callback for the ${event} event has already been registered.`);
        this.#callbacks.set(event, fn);
    }

    /**
     * Invokes a registered callback by its event name.
     * @param {string} event        The event to fire.
     * @param  {...any} eventArgs   Arguments to pass to the callback function.
     */
    #callCallback(event, ...eventArgs) {
        const fn = this.#callbacks.get(event);
        if(!fn) return;
        fn(...eventArgs);
    }

    //#endregion


    //#region Next Frame Awaits

    /** @type {{promise: Promise, resolve: Function, reject: Function}[]} */
    #nextFrameAwaitsQueue = [];

    /**
     * Returns a promise that resolves after the once frame has finished rendering.
     * @returns {Promise<void>}
     */
    async awaitNextFrame() {
        let resolve, reject;

        const promise = new Promise((res, rej) => {
            resolve = res;
            reject = rej;
        });

        this.#nextFrameAwaitsQueue.push({ promise, resolve, reject });
        return promise;
    }

    /**
     * Resolves all waiting promises registered by `awaitNextFrame`.
     */
    #resolveNextFramePromises() {
        while(this.#nextFrameAwaitsQueue.length > 0) {
            const deferred = this.#nextFrameAwaitsQueue.pop();
            deferred.resolve();
        }
    }

    //#endregion


    //#region Rendering

    /** 
     * A queue of rendering tasks to be performed on the next frame.
     */
    #nextFrameData = {
        created: new Set(),
        deleted: new Set(),
        updated: new Set(),
    }

    /**
     * Queues a rendering operation for a set of bodies. The operation will be
     * executed at the beginning of the next render cycle.
     * @param {"created"|"deleted"|"updated"} category      The type of operation to perform.
     * @param {Iterable<number>} bodyIds                    An iterable of body IDs to apply the operation to.
     */
    addFrameData(category, bodyIds) {
        if(!["created", "updated", "deleted"].includes(category)) {
            throw new Error(`Invalid argument 'category': ${category}`);
        }

        const catSet = this.#nextFrameData[category];
        for(const id of bodyIds) catSet.add(id);
    }

    /** 
     * Processes all queued rendering operations for the current frame and clears the queue.
     */
    #handleRender() {
        for(const id of this.#nextFrameData.created) this.#createBodyToken(id);
        for(const id of this.#nextFrameData.deleted) this.#deleteBodyToken(id);
        for(const id of this.#nextFrameData.updated) this.#updateBodyToken(id);

        this.#nextFrameData.created.clear();
        this.#nextFrameData.deleted.clear();
        this.#nextFrameData.updated.clear();
    }

    /**
     * Creates a new `BodyToken` and adds its sprite to the scene.
     * @param {number} id   The ID of the body to create a token for.
     */
    #createBodyToken(id) {
        try {
            const physicsData = this.#bodyStateDataStore.get(id);
            if(!physicsData) throw new Error("Invalid body physics data.");

            const metaData = this.#bodyMetaDataStore.get(id);
            if(!physicsData) throw new Error("Invalid body meta data.");

            const bodyToken = new BodyToken(physicsData, metaData);
            this.#scene.addChild(bodyToken.sprite);
            this.#bodyTokens.set(id, bodyToken);
            bodyToken.updateSprite(this.#CONFIG.PIXELS_PER_AU);
        } catch (err) {
            this.#onError(err, {
                notifMsg: `Failed to create body token of body id=${id}.`
            });
        }
    }

    /**
     * Destroys a `BodyToken` and removes its sprite from the scene.
     * @param {number} id   The ID of the body whose token should be deleted.
     */
    #deleteBodyToken(id) {
        try {
            const bodyToken = this.#bodyTokens.get(id);
            bodyToken.sprite.destroy();
            this.#scene.removeChild(bodyToken.sprite);
            this.#bodyTokens.delete(id);
        } catch (err) {
            this.#onError(err, {
                notifMsg: `Failed to delete body token of body id=${id}.`
            });
        }
    }

    /**
     * Updates an existing `BodyToken` based on the latest data from the injected stores.
     * @param {number} id   The ID of the body whose token should be updated.
     */
    #updateBodyToken(id) {
        try {
            const bodyToken = this.#bodyTokens.get(id);
            bodyToken.updateSprite(this.#CONFIG.PIXELS_PER_AU);
        } catch (err) {
            this.#onError(err, {
                notifMsg: `Failed to update body token of body id=${id}.`
            });
        }
    }

    //#endregion

    
    //#region Camera Constrols

    #isSceneDragging = false;
    #sceneDragPointerPosCache = {x: 0, y:0 };

    /**
     * Centers the view on a specific point.
     * @param {number} x                    The x-coordinate of the point to pan to.
     * @param {number} y                    The y-coordinate of the point to pan to.
     * @param {boolean} [isSimCoord=true]   If `true`, `x` and `y` are interpreted as simulation coordinates (AU).
     *                                      If `false`, they are interpreted as screen coordinates (pixels).
     * @returns {void}
     */
    panToPoint(x, y, isSimCoord=true) {
        const screenCenterX = this.#app.screen.width / 2;
        const screenCenterY = this.#app.screen.height / 2;

        let targetX, targetY;

        if(isSimCoord) {
            // Find where this simulation point exists in the scaled PIXI world.
            const worldX = x * this.#scene.scale.x;
            const worldY = y * this.#scene.scale.y;

            // To center this point, the scene's top-left corner must be offset
            // from the screen's center by that point's world coordinates.
            targetX = screenCenterX - worldX;
            targetY = screenCenterY - worldY;
        } else {
            const deltaX = screenCenterX - x;
            const deltaY = screenCenterY - y;

            targetX = this.#scene.x + deltaX;
            targetY = this.#scene.y + deltaY;
        }

        this.#scene.position.set(targetX, targetY);
    }

    /**
     * Pans the scene by a given pixel delta.
     * @param {number} dx   The change in x-position in pixels.
     * @param {number} dy   The change in y-position in pixels.
     */
    #panDelta(dx, dy) {
        this.#scene.x += dx;
        this.#scene.y += dy;
    }

    #onPointerDown(e) {
        if(e.button === 0) {
            this.#isSceneDragging = true;
            this.#sceneDragPointerPosCache.x = e.clientX;
            this.#sceneDragPointerPosCache.y = e.clientY;
        }
    }

    #onPointerUp(e) {
        if(e.button === 0) this.#isSceneDragging = false;
    }

    #onPointerMove(e) {
        if(this.#isSceneDragging) {
            const deltaX = e.clientX - this.#sceneDragPointerPosCache.x;
            const deltaY = e.clientY - this.#sceneDragPointerPosCache.y;
            this.#panDelta(deltaX, deltaY);

            this.#sceneDragPointerPosCache.x = e.clientX;
            this.#sceneDragPointerPosCache.y = e.clientY;
        }
    }

    #onPointerOut(e) {
        this.#isSceneDragging = false;
    }

    /**
     * Handles the 'wheel' event to zoom the scene in or out, centered on the mouse pointer.
     * @param {WheelEvent} e
     */
    #onWheel(e) {
        const {ZOOM_MAX, ZOOM_MIN, ZOOM_FACTOR} = this.#CONFIG;

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

    //#endregion
}

/** WORK IN PROGRESS */
class BodyToken {
    static #CONFIG = {
        disabledAlpha: 0.5,
        initRadius: 5,
    }

    /** @type {Texture} */
    static #bodyTexture;

    static get bodyTexture() { return this.#bodyTexture; };

    /**
     * @param {import("pixi.js").Renderer} renderer 
     */
    static staticInitBodyToken(renderer) {
        const circle = new Graphics()
            .circle(0, 0, this.#CONFIG.initRadius)
            .fill('white');

        this.#bodyTexture = renderer.generateTexture(circle);
    }

    /**
     * 
     * @param {BodyStateData} physicsData 
     * @param {BodyMetaData} metaData 
     */
    constructor(physicsData, metaData) {
        this.#physicsData = physicsData;
        this.#metaData = metaData;
    }

    #sprite = new Sprite(BodyToken.bodyTexture);

    /** @type {BodyStateData} */
    #physicsData;
    /** @type {BodyMetaData} */
    #metaData;

    get sprite() { return this.#sprite; }

    get radius() {
        // just return a fixed number for now until I figure out the physics
        return 5;
    }

    updateSprite(sceneScale=100) {
        this.#sprite.position.set(
            this.#physicsData.posX * sceneScale, 
            this.#physicsData.posY * sceneScale
        );

        this.#sprite.tint = this.#metaData.tint;
        this.#sprite.alpha = this.#physicsData.enabled ? 1 : BodyToken.#CONFIG.disabledAlpha;

        this.#sprite.scale.set(this.radius / BodyToken.#CONFIG.initRadius);
    }
}