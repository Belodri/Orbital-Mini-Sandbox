/**
 * Represents the types of values that can be submitted by a form element.
 * Compatible with {@link ElementInternals.setFormValue}.
 */
export type FormValue = string | File | FormData | null;

/** Configuration object to generate a base custom form element class. */
export interface BaseCustomFormElementConfig {
    /** The HTML tag name to be used when registering the custom element. */
    tagName: string;
    /** A list of attributes to watch for changes (triggers `attributeChangedCallback`). */
    observedAttributes: ReadonlyArray<string>;
    /** When a non-focusable part of the shadow DOM is clicked, should the first focusable part be given focus?  */
    delegatesFocus: boolean;
    /** If `true`, setting the value of the element emits an `input` and a `change` event. */
    setValueEmitsEvents: boolean;
}

/**
 * An abstract base class that implements the fundamental requirements  
 * for a Form-Associated Custom Element.
 */
abstract class CustomFormElement extends HTMLElement {
    static readonly formAssociated = true;
    constructor() {
        super();
        this.internals = this.attachInternals();
    }

    protected readonly internals: ElementInternals;
    /** The value of the element's "name" attribute. */
    get name(): string | null { return this.getAttribute("name"); }
    set name(value: string) { this.setAttribute("name", value); }
    /** The form this element is associated with. */
    get form(): HTMLFormElement | null { return this.internals.form; }
    /** Returns the string "type" of this form control (defaults to the local tag name). */
    get type(): string { return this.localName; }
    /** The `ValidityState` object for this element. */
    get validity(): ValidityState {return this.internals.validity; }
    /** The validation message (if any). */
    get validationMessage(): string {return this.internals.validationMessage; }
    /** `true` if the element will be validated when the form is submitted. */
    get willValidate(): boolean {return this.internals.willValidate; }
    /** 
     * Checks the validity of the element.  
     * If `false`, a cancelable `invalid` event is fired on the element.
     */
    checkValidity(): boolean { return this.internals.checkValidity(); }
    /**
     * Checks validity and reports the result to the user (usually via a browser tooltip)
     * if the element is invalid.  
     * If `false`, a cancelable `invalid` event is fired on the element.
     */
    reportValidity(): boolean { return this.internals.reportValidity(); }
    /**
     * Sets the validity of the element. If called without arguments, the element is assumed to meet constraint validation rules. 
     * @param flags     A dictionary object containing one or more flags indicating the validity state of the element.
     * @param message   A string containing a message, which will be set if any flags are true. This parameter is only optional if all flags are false.
     * @param anchor    An HTMLElement which can be used by the user agent to report problems with this form submission.
     */
    protected setValidity(flags: ValidityStateFlags = {}, message?: string, anchor?: HTMLElement): void {
        this.internals.setValidity(flags, message, anchor);
    }
    /**
     * Updates the form value associated with this element.
     * @param value     The value to submit with the form.
     * @param state     A File, a string, or a FormData representing the input made by the user. 
     *                  This allows the application to re-display the information that the user submitted, 
     *                  in the form that they submitted it, if required.
     */
    protected setFormValue(value: FormValue, state?: FormValue) {
        this.internals.setFormValue(value, state);
    }
}

/**
 * A mixin that creates an abstract class extending `CustomFormElement` with structured lifecycle management.
 * 
 * Standard Web Component callbacks `connectedCallback` and `disconnectedCallback` are sealed,
 * but call specific template methods (`_buildChildren`, `_refresh`, `_activateListeners`, and `_onDisconnect`).
 * 
 * Note: The element's value is not automatically set as the form value so subclasses must implement this themselves!
 * 
 * @template TValue     The type of the `value` property of the element.
 * @template TConfig    The configuration type ensuring strict typing for observed attributes.
 * 
 * @param cfg           Configuration object containing the tag name and observed attributes.
 * @returns             A class constructor of an abstract class that can be extended to create a custom element.
 */
export function GetBaseCustomFormElement<TValue, const TConfig extends BaseCustomFormElementConfig>(cfg: TConfig) {
    /**
     * Abstract class that extends `CustomFormElement` with structured lifecycle management, 
     * static attributes derived from the config, and strict typing.
     * 
     * Standard Web Component callbacks `connectedCallback` and `disconnectedCallback` are sealed,
     * but call specific template methods (`_buildChildren`, `_refresh`, `_activateListeners`, and `_onDisconnect`).
     */
    abstract class BaseCustomFormElement extends CustomFormElement {
        /** The HTML tag name to be used when registering the custom element. */
        static readonly tagName: TConfig["tagName"] = cfg.tagName;
        /** A list of attributes to watch for changes (triggers attributeChangedCallback). */
        static readonly observedAttributes: TConfig["observedAttributes"] = cfg.observedAttributes;

        #abortController?: AbortController;

        /** 
         * An `AbortSignal` that is active for the lifespan of the connection and is aborted automatically in `disconnectedCallback`.
         * To be used for for event listeners or async operations that should stop when the element is removed.
         */
        get abortSignal(): AbortSignal | undefined { return this.#abortController?.signal; }

        /** The canonical, underlying value of the element. */
        protected _value: TValue | undefined;

        /** The current value of the element. */
        get value() { return this._value; }
        set value(v) {
            if(v === this._value) return;
            const oldValue = this._value;
            this._value = v;
            this._onSetValue(oldValue);
        }

        /** 
         * Called after the value of the element has been set.  
         * Dispatches `input` and `change` events (if configured), and calls `_refresh()`
         * 
         * @param oldValue The previous value of the element.
         */
        protected _onSetValue(oldValue: TValue | undefined): void {
            if(cfg.setValueEmitsEvents) {
                this.dispatchEvent(new Event("input", { bubbles: true, cancelable: true, composed: true }));
                this.dispatchEvent(new Event("change", { bubbles: true, cancelable: true, composed: true }));
            }
            this._refresh();
        }

        /** Whether this element is disabled. */
        get disabled() { return this.matches(":disabled"); }
        set disabled(value) { this.toggleAttribute("disabled", value); }

        // Sealed Lifecycle methods

        /** 
         * @sealed
         * @final
         * **DO NOT OVERRIDE**
         * 
         * Orchestrates initialization:
         * 1. Creates a new AbortController.
         * 2. Calls `_buildChildren` and populates the shadow DOM with the returned elements.
         * 3. Calls `_refresh`.
         * 4. Calls `_activateListeners`.
         */
        connectedCallback(): void {
            this.#abortController = new AbortController();
            
            if(!this.shadowRoot) {
                this.attachShadow({
                    mode: "open",
                    delegatesFocus: cfg.delegatesFocus,
                });

                const elements = this._buildChildren();
                this.shadowRoot!.replaceChildren(...elements); // Non-null assertion is safe because of encapsulation mode = "open" in attachShadow
            }
            
            this._refresh();
            this._toggleDisabled(this.disabled);
            this._activateListeners();
        }

        /** 
         * @sealed
         * @final
         * **DO NOT OVERRIDE**
         * 
         * Orchestrates cleanup:
         * 1. Aborts the `abortSignal`.
         * 2. Calls `_disconnectCleanup`.
         */
        disconnectedCallback(): void {
            this.#abortController?.abort();
            this._disconnectCleanup();
        }

        /** 
         * @sealed
         * @final
         * **DO NOT OVERRIDE**
         * 
         * Calls `_toggleDisabled` only if the element is connected.
         */
        formDisabledCallback(disabled: boolean): void {
            if(this.isConnected) this._toggleDisabled(disabled);
        }

        // Callbacks

        /**
         * Called after the form is reset. The element should reset itself to some kind of default state. 
         */
        formResetCallback(): void {}
        /** 
         * Called when observed attributes are changed, added, removed, or replaced.  
         * Note that DOM access must be guarded as this callback can be called before the shadow DOM has been created.
         */
        attributeChangedCallback(attrName: TConfig["observedAttributes"][number], oldValue: string | null, newValue: string | null): void {}
        /** 
         * Called when the element is moved in the DOM. 
         * 
         * The presence of this method ensures that `connectedCallback` and `disconnectedCallback` do not run when the element is only moved.
         */
        connectedMoveCallback(): void {}

        // Template methods

        /** 
         * Create the children of this element, which override any existing children.
         */
        protected _buildChildren(): HTMLElement[] { return []; }
        /** Refresh the state of the element and its children. */
        protected _refresh(): void {}
        /** Activates any event listeners on the element or its children. */
        protected _activateListeners(): void {};
        /** Cleanup when the element is disconnected from the DOM. */
        protected _disconnectCleanup(): void {};
        /** 
         * Called after the `disabled` state of the element changes, either because the disabled attribute of this element 
         * was added or removed; or because the disabled state changed on a <fieldset> that's an ancestor of this element.
         * 
         * Only called if the element is connected.
         */
        protected _toggleDisabled(disabled: boolean): void {};
    }

    return BaseCustomFormElement;
}

/**
 * Registers a custom form element with the browser's `customElements` registry.
 * Uses the static `tagName` property found on the constructor.
 * 
 * @param ctor  Class constructor that inherits from the constructor generated by {@link GetBaseCustomFormElement}.
 */
export function registerCustomFormElement(ctor: { tagName: string } & CustomElementConstructor & typeof CustomFormElement) {
    customElements.define(ctor.tagName, ctor);
}

/** Type guard to check if a value is an instance that inherits from from the constructor generated by {@link GetBaseCustomFormElement}. */
export function isCustomFormElement(v: any): v is CustomFormElement {
    // This test works as long as `GetBaseCustomFormElement` is the only way to create a class that inherits from CustomFormElement!
    return v instanceof CustomFormElement;
}
