import { Application, Container, ContainerChild } from "pixi.js";
import { PIXI_HANDLER_CONFIG } from "./PixiHandler";

/**
 * Purpose-built component for the {@link PixiHandler} which handles panning and zooming the camera.
 * Registers event listeners to the `canvas` of the injected {@link Application}.
 *
 * Assumes that the `Application` has been properly initialized beforehand!
 */
export class CameraControls {
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

        let targetX: number, targetY: number;

        if (isSimCoord) {
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
        const { x, y } = this.#scene;
        this.#scene.position.set(x + dx, y + dy);
    }

    #onPointerDown = (e: PointerEvent) => {
        if (e.button === 0) {
            this.#isDragging = true;
            this.#dragPointerPos.x = e.clientX;
            this.#dragPointerPos.y = e.clientY;
        }
    };

    #onPointerUp = (e: PointerEvent) => {
        if (e.button === 0) this.#isDragging = false;
    };

    #onPointerMove = (e: PointerEvent) => {
        if (this.#isDragging) {
            const deltaX = e.clientX - this.#dragPointerPos.x;
            const deltaY = e.clientY - this.#dragPointerPos.y;
            this.panDelta(deltaX, deltaY);

            this.#dragPointerPos.x = e.clientX;
            this.#dragPointerPos.y = e.clientY;
        }
    };

    #onPointerOut = () => {
        this.#isDragging = false;
    };

    /** Handles the 'wheel' event to zoom the scene in or out, centered on the mouse pointer. */
    #onWheel = (e: WheelEvent) => {
        const { ZOOM_MAX, ZOOM_MIN, ZOOM_FACTOR } = PIXI_HANDLER_CONFIG;

        const zoomDirection = e.deltaY > 0 ? -1 : 1;
        const newScale = this.#scene.scale.x + (zoomDirection * ZOOM_FACTOR);
        if (newScale < ZOOM_MIN || newScale > ZOOM_MAX) return;

        const mousePos = { x: e.offsetX, y: e.offsetY };
        const scenePosPreZoom = this.#scene.toLocal(mousePos);

        this.#scene.scale.set(newScale);

        const scenePosPostZoom = this.#scene.toLocal(mousePos);

        this.#scene.x -= (scenePosPostZoom.x - scenePosPreZoom.x) * newScale;
        this.#scene.y -= (scenePosPostZoom.y - scenePosPreZoom.y) * newScale;
    };
}
