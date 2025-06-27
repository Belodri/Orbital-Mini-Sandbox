import { Application, Container, Graphics, UPDATE_PRIORITY } from "pixi.js";
import AppShell from "../AppShell.mjs";

export default class CanvasView {
    /**
     * 
     * @param {Partial<import("pixi.js").ApplicationOptions>} initArgs 
     * @returns {Promise<CanvasView>}
     */
    static async create(initArgs = {}) {
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
        return new CanvasView(app);
    }

    destroy() {
        this.#app.destroy(true, true);
    }

    /**
     * 
     * @param {Application} app 
     */
    constructor(app) {
        this.#app = app;
        document.body.appendChild(this.#app.canvas);

        this.#scene = new Container();
        this.#app.stage.addChild(this.#scene);

        this.#initRenderLoop();
    }

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

    /** @type {Application} */
    #app;

    #isStopped = true;

    get isStopped() { return this.#isStopped; }

    /**
     * 
     * @param {boolean} [force=undefined]
     * @returns {boolean} 
     */
    toggleStop(force=undefined) {
        this.#isStopped = force === undefined
            ? !this.#isStopped
            : !!force;
        if(this.#isStopped) this.#app.stop();
        else this.#app.start();
        return this.#isStopped;
    }

    //#region Render Loop

    get ticker() { return this.#app.ticker; }

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
            for(const id of this.#tickDiff.created) this.#createBodyToken(id);
            for(const id of this.#tickDiff.deleted) this.#deleteBodyToken(id);
            for(const id of this.#tickDiff.updated) this.#updateBodyToken(id);
        }, this, UPDATE_PRIORITY.NORMAL);
    }

    //#endregion

    //#region Render Loop Body Updates

    #createBodyToken(id) {
        const bodyToken = new BodyToken(id);
        bodyToken.onCreate();
        this.#scene.addChild(bodyToken.graphics);
        this.#bodyTokens.set(id, bodyToken);
        bodyToken.updateGraphics();
    }

    #deleteBodyToken(id) {
        const bodyToken = this.#bodyTokens.get(id);
        bodyToken.onDelete();
        this.#scene.removeChild(bodyToken.graphics);
        this.#bodyTokens.delete(id);
    }

    #updateBodyToken(id) {
        const bodyToken = this.#bodyTokens.get(id);
        bodyToken.onUpdate({isPhysics: true, isMeta: false});
        bodyToken.updateGraphics();
    }

    //#endregion
}

class BodyToken {
    /** @type {number} */
    #id;

    #graphics = new Graphics();

    get graphics() { return this.#graphics; }

    constructor(id) {
        this.#id = id;
        this.#physicsData = AppShell.Bridge.simState.bodies.get(this.#id);
        this.#metaData = AppShell.appDataManager.bodyData.get(this.#id);
    }

    /** @type {import("../../types/Bridge").BodyStateData} */
    #physicsData;
    /** @type {import("../AppDataManager.mjs").BodyMetaData} */
    #metaData;

    get radius() {
        // just return a fixed number for now until I figure out the physics
        return 5;
    }

    //#region CRUD Events

    /** Called when the data of the body is updated */
    onUpdate({isPhysics = true, isMeta = true}={}) {
        if(isPhysics) this.#physicsData = AppShell.Bridge.simState.bodies.get(this.#id);
        if(isMeta) this.#metaData = AppShell.appDataManager.bodyData.get(this.#id);
    }

    onCreate() {
        this.#graphics
            .circle(0, 0, this.radius)
            .fill(this.#metaData.color);
    }

    onDelete() {
        this.#graphics.destroy(true);
    }

    //#endregion
    

    //#region Rendering

    /**
     * Called once each render loop. 
     */
    updateGraphics() {
        this.#graphics.x = this.#physicsData.posX;
        this.#graphics.y = this.#physicsData.posY;
    }
    
    //#endregion
}