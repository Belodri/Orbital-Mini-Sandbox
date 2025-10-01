type VMBaseHeaderEles = {
    collapseBtn: HTMLButtonElement;
    titleSpan: HTMLSpanElement;
    closeBtn: HTMLButtonElement;
}

type ViewModelConfig = {
    id?: string;
    cssClasses: string[];
    startCollapsed: boolean;
    title?: string
}

export abstract class ViewModel<HeaderElements extends Record<string, HTMLElement> = {}, BodyElements extends Record<string, HTMLElement> = {}> {

    protected get CSS_CLASSES() {
        return { BASE_CLASSES: ["vm"], COLLAPSED: "hidden" }
    }

    static #nextID: number = 0;

    #id: string;
    #collapsed: boolean = false;
    #base: HTMLDivElement;
    #header: HTMLDivElement;
    #body: HTMLDivElement;

    /** Collection of references to HTML elements. */
    protected readonly _eles: HeaderElements & BodyElements & VMBaseHeaderEles;

    /** The HTML element ID of this ViewModel instance. */
    get id(): string { return this.#id; }
    /** The base element which renders this ViewModel into the DOM. */
    get base() { return this.#base; }
    get collapsed() { return this.#collapsed; }
    /** The header element of this ViewModel. Direct child of #element. */
    protected get header() { return this.#header; }
    /** The body element of this ViewModel. Direct child of #element. */
    protected get body() { return this.#body; }

    //#region Constructor

    constructor(cfg: ViewModelConfig) {
        this.#id = cfg.id ?? String(++ViewModel.#nextID);

        const { base, header, body } = this.#createCoreElements(cfg);
        
        const headerEles = {
            ...this.#baseHeaderElesToAppend(cfg),
            ...this._headerElementsToAppend(cfg)
        };
        const bodyEles = this._bodyElementsToAppend(cfg);

        const allEles = {...headerEles, ...bodyEles};

        for(const [k, v] of Object.entries(allEles) as [string, HTMLElement][]) {
            v.classList.add(...this.CSS_CLASSES.BASE_CLASSES);
            if(k in headerEles) header.appendChild(v);
            else body.appendChild(v);
        }

        this.#base = base;
        this.#body = body;
        this.#header = header;
        this._eles = allEles;
    }

    #createCoreElements(cfg: ViewModelConfig): { base: HTMLDivElement, header: HTMLDivElement, body: HTMLDivElement } {
        const cssBase = this.CSS_CLASSES.BASE_CLASSES;

        const base = document.createElement("div");
        base.classList.add(...cssBase, ...cfg.cssClasses);
        base.id = this.#id;
        
        const header = document.createElement("div");
        header.classList.add(...cssBase);
        base.appendChild(header);

        const body = document.createElement("div");
        body.classList.add(...cssBase);
        base.appendChild(body);

        return { base, header, body };
    }

    #baseHeaderElesToAppend(cfg: ViewModelConfig): VMBaseHeaderEles {
        const titleSpan = document.createElement("span");
        if(cfg.title) titleSpan.textContent = cfg.title;
        
        const collapseBtn = document.createElement("button");
        if(cfg.startCollapsed) {
            collapseBtn.classList.add(this.CSS_CLASSES.COLLAPSED);
            this.#collapsed = true;
        }
        collapseBtn.addEventListener("pointerdown", (event) => {
            event.stopPropagation();
            this.toggleCollapse();
        });
        
        const closeBtn = document.createElement("button");
        closeBtn.addEventListener("pointerdown", (event) => {
            event.stopPropagation();
            this.destroy();
        });
        
        return { titleSpan, collapseBtn, closeBtn };
    }

    /**
     * Called by the constructor to create elements that are then appended to the header.
     * @param cfg The config object used to create this instance.
     */
    protected abstract _headerElementsToAppend(cfg: ViewModelConfig): HeaderElements;

    /**
     * Called by the constructor to create elements that are then appended to the body.
     * @param cfg The config object used to create this instance.
     */
    protected abstract _bodyElementsToAppend(cfg: ViewModelConfig): BodyElements;


    //#endregion

    /**
     * Toggles the collapsed state of the ViewModel's body element.
     * @param force If given, forces the collapsed state to the given value instead.
     */
    toggleCollapse(force?: boolean) {
        const newState = typeof force === "boolean" ? force : !this.#collapsed;
        if(newState !== this.#collapsed) {
            this.#body.classList.toggle(this.CSS_CLASSES.COLLAPSED, newState);
            this.#collapsed = newState;
        }
    }

    /** Destroys the ViewModel and cleans up references to it and its child elements. */
    destroy() {
        this.#base?.remove();    // Safeguard against multiple destroy() calls;
        for(const k in this._eles) (this._eles as Record<string, HTMLElement>)[k] = undefined as any;
        this.#body = undefined as any;
        this.#header = undefined as any;
        this.#base = undefined as any;
    }
}

export abstract class ViewModelMovable
    <HeaderElements extends Record<string, HTMLElement> = {}, BodyElements extends Record<string, HTMLElement> = {}> 
    extends ViewModel<HeaderElements, BodyElements> {
    
    protected override get CSS_CLASSES() {
        const parentCss = super.CSS_CLASSES;
        return { 
            ...parentCss, 
            BASE_CLASSES: [...parentCss.BASE_CLASSES, "movable"],
            IS_DRAGGING: "is-dragging",
        }
    }

    static #topZ = 101;     // See Docs/Front_End_Alpha.md for reasoning behind hardcoding this value.

    /** The id of the element that is or was in front, which might no longer exist. */
    static #frontId: string = "";

    /** Gets a z-index number that is higher than all other ViewModelMovable instances. */
    static getTopZ(): number { return this.#topZ++; }

    #boundHandlers = {
        onDragMove: this.#onDragMove.bind(this),
        onDragEnd: this.#onDragEnd.bind(this),
        bringToFront: this.bringToFront.bind(this),
        onDragStart: this.#onDragStart.bind(this)
    }

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
        if(ViewModelMovable.#frontId === this.id) return;
        this.base.style.zIndex = String(ViewModelMovable.getTopZ());
        ViewModelMovable.#frontId = this.id;
    }

    #onDragStart(event: PointerEvent) {
        const rect = this.base.getBoundingClientRect();
        this.#dragStartPosition = {
            x: event.clientX - rect.left,
            y: event.clientY - rect.top
        }

        this.#isDragging = true;
        document.body.classList.add(this.#isDraggingCssClass);
        document.addEventListener("pointermove", this.#boundHandlers.onDragMove);
        document.addEventListener("pointerup", this.#boundHandlers.onDragEnd, { once: true });
    }

    #onDragMove(event: PointerEvent) {
        if(!this.#isDragging) return;

        window.requestAnimationFrame(() => {
            const {x, y} = this.#dragStartPosition;
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
        if(this.#isDragging) this.#onDragEnd();
        super.destroy();
    }
}
