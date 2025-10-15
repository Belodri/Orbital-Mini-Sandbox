import { Parts } from "../App";

export interface IRenderLoop {
    paused: boolean;
    /**
     * Toggles or sets the paused state of the simulation.
     * @param force     If provided, sets the paused state directly (`true` for paused, `false` for running). If omitted, the state is toggled.
     */
    togglePause(force?: boolean): void;
    /** Orchestrates physics calculations and data transformations. Called by PixiHandler at the very beginning of a new render frame. */
    preRender(): void;
    /** Orchestrates rendering. Called by PixiHandler after preRender tasks are completed. */
    render(): void;
    /** Orchestrates resolving of deferred API promises. Called by PixiHandler after rendering is fully complete. */
    postRender(): void;
    /** Starts the render loop. */
    start(): void;
    /** Stops the render loop. */
    stop(): void;
}

export default class RenderLoop implements IRenderLoop {
    #parts: Parts;

    constructor(parts: Parts) {
        this.#parts = parts;
    }

    #paused: boolean = false;
    get paused() { return this.#paused; }

    togglePause = (force?: boolean) => {
        const newState = typeof force === "boolean" ? force : !this.#paused;
        this.#paused = newState;
    };

    preRender = () => {
        try {
            const syncOnly = this.#paused;
            this.#parts.bridge.tickEngine(syncOnly);

            this.#parts.appData.syncDiff(
                this.#parts.bridge.diff.bodies.created,
                this.#parts.bridge.diff.bodies.deleted
            );

            this.#parts.views.refresh(
                this.#parts.bridge.state,
                this.#parts.bridge.diff,
                this.#parts.appData.state,
                this.#parts.appData.diff
            );
        } catch (err) {
            this.#onFatalError(err, `Fatal Pre-Render Error.`);
        }
    };

    render = () => {
        try {
            this.#parts.uiManager.render(this.#parts.views);
        } catch (err) {
            this.#onFatalError(err, `Fatal Render Error.`);
        }
    };

    postRender = () => {
        try {
            this.#parts.resolver.resolve();
        } catch (err) {
            this.#onFatalError(err, `Fatal Post-Render Error.`);
        }
    };

    start = () => this.#parts.pixi.start();
    stop = () => this.#parts.pixi.stop();

    #onFatalError(err: unknown, notif: string) {
        this.stop();
        console.error(err);
        this.#parts.notif.error(notif);
    }
}
