import { Application, Container, CullerPlugin, extensions, Graphics, Sprite, Texture, UPDATE_PRIORITY } from "pixi.js";
import AppShell from "../AppShell.mjs";

/**
 * Pixi canvas controller.
 * Use as a singleton only! The elements and listeners managed by an instance of this class are not destroyed and could cause memory leaks!  
 */
export default class CanvasView {
    //#region PUBLIC

    /**
     * 
     * @param {Partial<import("pixi.js").ApplicationOptions>} initArgs 
     * @returns {Promise<CanvasView>}
     */
    static async create(initArgs = {}) {
        if(CanvasView.#instance) throw new Error("CanvasView has already been created.");

        const app = new Application();
        const appArgs = {
            autoStart: false,
            resizeTo: window,
            backgroundColor: "black",
            preference: /** @type {'webgpu'} */ ('webgpu'),
            powerPreference: /** @type {'high-performance'} */ ('high-performance'),
            ...initArgs
        };

        await app.init(appArgs);
        extensions.add(CullerPlugin);

        CanvasView.#instance = new CanvasView(app);
        return CanvasView.#instance;
    }

    get renderLoopStopped() { return this.#renderLoopStopped; }

    /**
     * 
     * @param {boolean} [force=undefined]
     * @returns {boolean} 
     */
    toggleStop(force=undefined) {
        this.#renderLoopStopped = force === undefined
            ? !this.#renderLoopStopped
            : !!force;
        if(this.#renderLoopStopped) this.#app.stop();
        else this.#app.start();
        return this.#renderLoopStopped;
    }

    //#endregion

    /**
     * 
     * @param {Application} app 
     */
    constructor(app, config={}) {
        this.#CONFIG = {
            ...this.#CONFIG,
            ...config
        };

        this.#app = app;
        document.body.appendChild(this.#app.canvas);
        BodyToken.staticInitBodyToken(this.#app.renderer)
        this.#initScene();
        this.#registerEventListeners();
        this.#initRenderLoop();
    }

    /** @type {CanvasView} */
    static #instance;

    #CONFIG = {
        ZOOM_MIN: 0.01,
        ZOOM_MAX: 10000,
        ZOOM_FACTOR: 0.05,
        /** @type {number} How many pixels is 1 AU? */
        PIXELS_PER_AU: 100,
    }

    /** @type {Application} */
    #app;

    /** @type {Container} */
    #scene;

    /** @type {Map<number, BodyToken>} */
    #bodyTokens = new Map();

    /** @type {import("../../types/Bridge").BodyDiffData} */
    #tickDiff = {
        created: new Set(),
        deleted: new Set(),
        updated: new Set(),
    }

    /** @type {Set<number>} */
    #updateBodyTokenQueue = new Set();

    #renderLoopStopped = true;

    /** Is the scene currently being dragged? */
    #isSceneDragging = false;

    #sceneDragPointerPosCache = {x: 0, y:0 };

    #registerEventListeners() {
        this.#app.canvas.addEventListener("pointerdown", this.#onPointerDown.bind(this));
        this.#app.canvas.addEventListener("pointerup", this.#onPointerUp.bind(this));
        this.#app.canvas.addEventListener("pointermove", this.#onPointerMove.bind(this));
        this.#app.canvas.addEventListener("pointerout", this.#onPointerOut.bind(this));
        this.#app.canvas.addEventListener("wheel", this.#onWheel.bind(this), { passive: true });
    }

    #initRenderLoop() {
        this.#app.ticker.add((ticker) => {
            try {
                this.#tickDiff = AppShell.Bridge.tickEngine(ticker.deltaTime);
            } catch (err) {
                this.toggleStop(true);
                console.error(err);
                AppShell.notifications.add(`Physics Error.`);
            }            
        }, this, UPDATE_PRIORITY.HIGH);

        this.#app.ticker.add((ticker) => {
            if(this.#fullReRender) {
                for(const id of this.#bodyTokens.keys()) this.#deleteBodyToken(id);
                for(const id of AppShell.appDataManager.bodyData.keys()) this.#createBodyToken(id);
            } else {
                for(const id of this.#tickDiff.created) this.#createBodyToken(id);
                for(const id of this.#tickDiff.deleted) this.#deleteBodyToken(id);
                for(const id of this.#tickDiff.updated) this.#updateBodyToken(id);
                for(const id of this.#updateBodyTokenQueue) this.#updateBodyToken(id);   //self clearing
            }
        }, this, UPDATE_PRIORITY.NORMAL);
    }

    #fullReRender = false;

    queueFullReRender() {
        this.#fullReRender = true;
    }

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
     * Manually queue a body's token to be updated on the next frame, 
     * even if its simulation data hasn't changed.
     * @param {number} id 
     */
    queueBodyUpdate(id) {
        this.#updateBodyTokenQueue.add(id);
    }

    #createBodyToken(id) {
        const physicsData = AppShell.Bridge.simState.bodies.get(id);
        const metaData = AppShell.appDataManager.bodyData.get(id);
        const bodyToken = new BodyToken(physicsData, metaData);
        this.#scene.addChild(bodyToken.sprite);
        this.#bodyTokens.set(id, bodyToken);
        bodyToken.updateSprite(this.#CONFIG.PIXELS_PER_AU);
    }

    #deleteBodyToken(id) {
        const bodyToken = this.#bodyTokens.get(id);
        bodyToken.sprite.destroy();
        this.#scene.removeChild(bodyToken.sprite);
        this.#bodyTokens.delete(id);
    }

    #updateBodyToken(id) {
        const bodyToken = this.#bodyTokens.get(id);
        bodyToken.updateSprite(this.#CONFIG.PIXELS_PER_AU);
        this.#updateBodyTokenQueue.delete(id);
    }

    //#endregion

    
    //#region Camera Constrols

    /**
     * Pans the scene to the specified screen corrdinates.
     * @param {number} x 
     * @param {number} y
     * @param {boolean} [isSimCoord=true] If true, x/y are simulation coordinates. If false, they are screen coordinates.
     * @returns {void} 
     */
    _panToPoint(x, y, isSimCoord=true) {
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
     * Pans the scene by a delta.
     * @param {number} dx 
     * @param {number} dy 
     * @returns {void}
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

    #onWheel(e) {
        e.preventDefault();

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

    constructor(physicsData, metaData) {
        this.#physicsData = physicsData;
        this.#metaData = metaData;
    }

    #sprite = new Sprite(BodyToken.bodyTexture);

    /** @type {import("../../types/Bridge").BodyStateData} */
    #physicsData;
    /** @type {import("../AppDataManager.mjs").BodyMetaData} */
    #metaData;

    get sprite() { return this.#sprite; }

    get radius() {
        // just return a fixed number for now until I figure out the physics
        return 5;
    }

    /**
     * Called once each render loop. 
     */
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