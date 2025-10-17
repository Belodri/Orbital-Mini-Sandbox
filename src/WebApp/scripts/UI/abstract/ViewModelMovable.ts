import ViewModel, { type ViewModelConfig, IViewModel } from "./ViewModel";

export const CSS_CLASS_MOVABLE = "movable-view" as const;

/** Css class added to the <body> element while a {@link ViewModelMovable} is being dragged. */
export const CSS_CLASS_DRAGGING = "is-dragging" as const;

export interface IViewModelMovable extends IViewModel {
    /** Brings the element of this ViewModel to the front, if it isn't already. */
    bringToFront(): void;
}

export default abstract class ViewModelMovable<TRenderArgs extends any[] = []> extends ViewModel<TRenderArgs> {
    static #topZ = 101; // See Docs/Front_End_Alpha.md for reasoning behind hardcoding this value.

    /** The id of the element that is or was in front, which might no longer exist. */
    static #frontId: string = "";

    static get isDragging() { return document.body.classList.contains(CSS_CLASS_DRAGGING); }

    /** Gets a z-index number that is higher than all other ViewModelMovable instances. */
    static getTopZ(): number { return ViewModelMovable.#topZ++; }

    #boundHandlers = {
        onDragMove: this.#onDragMove.bind(this),
        onDragEnd: this.#onDragEnd.bind(this),
        bringToFront: this.bringToFront.bind(this),
        onDragStart: this.#onDragStart.bind(this)
    };

    #dragStartPosition = { x: 0, y: 0 };
    #isDragging = false;

    constructor(cfg: ViewModelConfig) {
        super(cfg);

        this.container.classList.add(CSS_CLASS_MOVABLE);
        this.container.style.position = "absolute";

        const handle = this.getDragHandle();
        handle.addEventListener("pointerdown", this.#boundHandlers.bringToFront);
        this.container.addEventListener("pointerdown", this.#boundHandlers.onDragStart);
    }

    /** Subclasses can override this to specify a specific drag handle element. */
    protected getDragHandle(): HTMLElement {
        return this.container; // Defaults to the whole element
    }

    /** Brings the element of this ViewModel to the front, if it isn't already. */
    bringToFront() {
        if (ViewModelMovable.#frontId === this.id) return;
        this.container.style.zIndex = String(ViewModelMovable.getTopZ());
        ViewModelMovable.#frontId = this.id;
    }

    #onDragStart(event: PointerEvent) {
        const rect = this.container.getBoundingClientRect();
        this.#dragStartPosition = {
            x: event.clientX - rect.left,
            y: event.clientY - rect.top
        };

        this.#isDragging = true;
        document.body.classList.add(CSS_CLASS_DRAGGING);
        document.addEventListener("pointermove", this.#boundHandlers.onDragMove);
        document.addEventListener("pointerup", this.#boundHandlers.onDragEnd, { once: true });

        this._onDragStart();
    }

    #onDragMove(event: PointerEvent) {
        if (!this.#isDragging) return;

        window.requestAnimationFrame(() => {
            const { x, y } = this.#dragStartPosition;
            this.container.style.left = `${event.clientX - x}px`;
            this.container.style.top = `${event.clientY - y}px`;

            this._onDragMove();
        });
    }

    #onDragEnd() {
        if(!this.#isDragging) return;
        document.body.classList.remove(CSS_CLASS_DRAGGING);
        // "pointerup" removes itself, and the other events are cleaned when the GC
        // collects #element, so only "pointermove" must be removed explicitly.
        document.removeEventListener("pointermove", this.#boundHandlers.onDragMove);

        this._onDragEnd();
    }

    protected abstract _onDragStart(): void;

    protected abstract _onDragMove(): void;

    protected abstract _onDragEnd(): void;

    override destroy(): void {
        if(this.#isDragging) this.#onDragEnd();
        super.destroy();
    }
} 