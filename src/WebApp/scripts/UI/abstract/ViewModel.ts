type VMBaseHeaderEles = {
    collapseBtn: HTMLButtonElement;
    titleSpan: HTMLSpanElement;
    closeBtn: HTMLButtonElement;
}

export type ViewModelConfig = {
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
