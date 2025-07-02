/**
 * Pixi canvas controller.
 * Use as a singleton only! The elements and listeners managed by an instance of this class are not destroyed and could cause memory leaks!
 */
export default class CanvasView {
    /**
     *
     * @param {Partial<import("pixi.js").ApplicationOptions>} initArgs
     * @returns {Promise<CanvasView>}
     */
    static create(initArgs?: Partial<import("pixi.js").ApplicationOptions>): Promise<CanvasView>;
    /** @type {CanvasView} */
    static "__#1@#instance": CanvasView;
    /**
     *
     * @param {Application} app
     */
    constructor(app: Application, config?: {});
    get renderLoopStopped(): boolean;
    /**
     *
     * @param {boolean} [force=undefined]
     * @returns {boolean}
     */
    toggleStop(force?: boolean): boolean;
    queueFullReRender(): void;
    /**
     * Manually queue a body's token to be updated on the next frame,
     * even if its simulation data hasn't changed.
     * @param {number} id
     */
    queueBodyUpdate(id: number): void;
    /**
     * Pans the scene to the specified screen corrdinates.
     * @param {number} x
     * @param {number} y
     * @param {boolean} [isSimCoord=true] If true, x/y are simulation coordinates. If false, they are screen coordinates.
     * @returns {void}
     */
    _panToPoint(x: number, y: number, isSimCoord?: boolean): void;
    #private;
}
import { Application } from "pixi.js";
