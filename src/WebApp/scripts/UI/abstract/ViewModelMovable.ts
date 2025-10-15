import { ViewModel, ViewModelConfig } from "./ViewModel";


export abstract class ViewModelMovable<HeaderElements extends Record<string, HTMLElement> = {}, BodyElements extends Record<string, HTMLElement> = {}>
    extends ViewModel<HeaderElements, BodyElements> {

    protected override get CSS_CLASSES() {
        const parentCss = super.CSS_CLASSES;
        return {
            ...parentCss,
            BASE_CLASSES: [...parentCss.BASE_CLASSES, "movable"],
            IS_DRAGGING: "is-dragging",
        };
    }

    static #topZ = 101; // See Docs/Front_End_Alpha.md for reasoning behind hardcoding this value.


    /** The id of the element that is or was in front, which might no longer exist. */
    static #frontId: string = "";

    /** Gets a z-index number that is higher than all other ViewModelMovable instances. */
    static getTopZ(): number { return this.#topZ++; }

    #boundHandlers = {
        onDragMove: this.#onDragMove.bind(this),
        onDragEnd: this.#onDragEnd.bind(this),
        bringToFront: this.bringToFront.bind(this),
        onDragStart: this.#onDragStart.bind(this)
    };

    #dragStartPosition = { x: 0, y: 0 };
    #isDragging = false;
    #isDraggingCssClass: string;

    constructor(cfg: ViewModelConfig) {
        super(cfg);
        this.base.addEventListener("pointerdown", this.#boundHandlers.bringToFront);
        this.header.addEventListener("pointerdown", this.#boundHandlers.onDragStart);
        this.#isDraggingCssClass = this.CSS_CLASSES.IS_DRAGGING;
    }

    /** Brings the element of this ViewModel to the front, if it isn't already. */
    bringToFront() {
        if (ViewModelMovable.#frontId === this.id) return;
        this.base.style.zIndex = String(ViewModelMovable.getTopZ());
        ViewModelMovable.#frontId = this.id;
    }

    #onDragStart(event: PointerEvent) {
        const rect = this.base.getBoundingClientRect();
        this.#dragStartPosition = {
            x: event.clientX - rect.left,
            y: event.clientY - rect.top
        };

        this.#isDragging = true;
        document.body.classList.add(this.#isDraggingCssClass);
        document.addEventListener("pointermove", this.#boundHandlers.onDragMove);
        document.addEventListener("pointerup", this.#boundHandlers.onDragEnd, { once: true });
    }

    #onDragMove(event: PointerEvent) {
        if (!this.#isDragging) return;

        window.requestAnimationFrame(() => {
            const { x, y } = this.#dragStartPosition;
            this.base.style.left = `${event.clientX - x}px`;
            this.base.style.top = `${event.clientY - y}px`;
        });
    }

    #onDragEnd() {
        document.body.classList.remove(this.#isDraggingCssClass);
        // "pointerup" removes itself, and the other events are cleaned when the GC
        // collects #element, so only "pointermove" must be removed explicitly.
        document.removeEventListener("pointermove", this.#boundHandlers.onDragMove);
        this.#isDragging = false;
    }

    override destroy() {
        if (this.#isDragging) this.#onDragEnd();
        super.destroy();
    }
}
