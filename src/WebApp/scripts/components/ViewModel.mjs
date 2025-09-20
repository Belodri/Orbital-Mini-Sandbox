// @ts-nocheck

/**
 * A base type defining the core DOM element references for a ViewModel.
 * Subclasses can extend this type if needed.
 * @typedef {object} ViewModelRefs
 * @property {HTMLDivElement} body
 * @property {HTMLDivElement} header
 * @property {HTMLButtonElement} collapse
 * @property {HTMLSpanElement} title
 * @property {HTMLButtonElement} close
 */

/**
 * @template {ViewModelRefs} TRefs
 */
export class ViewModel {
    /** @type {number} */
    static #nextID = 0;

    static get CSS_CLASSES() {
        return {
            element: ["vm-element"],
            body: ["vm-body"],
            header: ["vm-header"],
            collapse: ["vm-toggle"],
            title: ["vm-title"],
            close: ["vm-close", "icon", "fa-solid", "fa-xmark"]
        }
    };

    /** @type {string} */
    #id;

    /** @type {HTMLElement} The HTMLElement which renders this ViewModel into the DOM. */
    #element;

    /** @type {boolean} */
    #collapsed = false;

    /** 
     * Collection of references to elements that should be removed when the ViewModel is destroyed.
     * The initial value is cast because subclasses will add to it, fulfilling
     * the generic TRefs contract upon full instantiation.
     * @type {TRefs}
     * @protected
     */
    _refs = /** @type {TRefs} */ ({});

    constructor() {
        this.#id = String(++ViewModel.#nextID); 
        
        const cssClasses = this.getInstanceConstructor.CSS_CLASSES;
        
        const element = document.createElement("div");
        element.classList.add(...cssClasses.element);
        element.id = this.#id;
        
        const header = document.createElement("div");
        header.classList.add(...cssClasses.header);
        element.appendChild(header);

        const body = document.createElement("div");
        body.classList.add(...cssClasses.body);
        element.appendChild(body);

        // Reference elements
        this.#element = element;
        this._refs.header = header;
        this._refs.body = body;

        this._createHeaderContent();
    }

    /** @type {string} The HTML element ID of this ViewModel instance. */
    get id() { return this.#id; }
    /** @type {HTMLElement} The HTMLElement which renders this ViewModel into the DOM. */
    get element() { return this.#element; }

    /**
     * @protected
     * @returns {TRefs} 
     */
    get refs() { return this._refs; }
    get collapsed() { return this.#collapsed; }
    
    /**
     * Gets the specific constructor function that was used to create this instance.
     * This allows access to static properties of the subclass.
     * @returns {typeof ViewModel}
     */
    get getInstanceConstructor() {
        return /** @type {typeof ViewModel} */ (this.constructor)
    }

    /** 
     * Creates `collapse` and `close` buttons, and `title` span for the header.
     * Subclasses can either override this method entirely or simply disable/hide individual header elements.
     * @protected
     */
    _createHeaderContent() {
        const css = this.getInstanceConstructor.CSS_CLASSES;

        const title = document.createElement("span");
        title.classList.add(...css.title)
        this._refs.header.prepend(title);
        
        const collapse = document.createElement("button");
        collapse.classList.add(...css.collapse);
        collapse.addEventListener("pointerdown", (event) => {
            event.stopPropagation();
            this.toggleCollapse();
        });
        this.refs.header.appendChild(collapse);
        
        const closeBtn = document.createElement("button");
        closeBtn.classList.add(...css.close);
        closeBtn.addEventListener("pointerdown", (event) => {
            event.stopPropagation();
            this.destroy();
        });
        this._refs.header.appendChild(closeBtn);
        
        // Add refs
        this._refs.title = title;
        this._refs.collapse = collapse;
        this._refs.close = closeBtn;
    }

    toggleCollapse(force=null) {
        const newState = typeof force === "boolean" ? force : !this.#collapsed;
        if(newState !== this.#collapsed) {
            this._refs.body.classList.toggle("hidden", newState);
            this.#collapsed = newState;
        }
    }

    destroy() {
        this.#element?.remove();
        for(const k of Object.keys(this._refs)) this._refs[k] = undefined;
        this.#element = undefined;
    }
}

export class ViewModelMovable extends ViewModel {
    /** @override */
    static get CSS_CLASSES() {
        const parentCSS = super.CSS_CLASSES;
        return {
            ...parentCSS,
            element: [...parentCSS.element, "vm-movable"]
        };
    }

    static #topZ = 10;
    /** @type {string} The id of the element that is or was in front, which might no longer exist. */
    static #frontId = "";

    /**
     * Gets a z-index number that is higher than all other ViewModelMovable instances.
     * @returns {number}
     */
    static getTopZ() { return this.#topZ++; }

    // Pre-bind drag event handlers that are attached to the document
    #boundOnDragMove = this.#onDragMove.bind(this);
    #boundOnDragEnd = this.#onDragEnd.bind(this);
    
    #dragStartPosition = { x: 0, y: 0 };
    #isDragging = false;

    constructor() {
        super();
        this.element.addEventListener("pointerdown", this.bringToFront.bind(this));
        this._refs.header.addEventListener("pointerdown", this.#onDragStart.bind(this));
    }

    /** Brings the element of this ViewModel to the front, if it isn't already. */
    bringToFront() {
        if(ViewModelMovable.#frontId === this.id) return;
        this.element.style.zIndex = String(ViewModelMovable.getTopZ());
        ViewModelMovable.#frontId = this.id;
    }

    /** @param {PointerEvent} event  */
    #onDragStart(event) {
        const rect = this.element.getBoundingClientRect();
        this.#dragStartPosition = {
            x: event.clientX - rect.left,
            y: event.clientY - rect.top
        }

        this.#isDragging = true;
        document.body.classList.add("is-dragging");
        document.addEventListener("pointermove", this.#boundOnDragMove);
        document.addEventListener("pointerup", this.#boundOnDragEnd, { once: true });
    }

    /** @param {PointerEvent} event  */
    #onDragMove(event) {
        if(!this.#isDragging) return;

        window.requestAnimationFrame(() => {
            const {x, y} = this.#dragStartPosition;
            this.element.style.left = `${event.clientX - x}px`;
            this.element.style.top = `${event.clientY - y}px`;
        });
    }

    #onDragEnd() {
        document.body.classList.remove("is-dragging");
        // "pointerup" removes itself and the other events are cleaned when the GC collects #element,
        // so only "pointermove" must be removed explicitly.
        document.removeEventListener("pointermove", this.#boundOnDragMove);
        this.#isDragging = false;
    }

    /** @override */
    destroy() {
        if(this.#isDragging) this.#onDragEnd();
        super.destroy();
    }
}
