import { ITypeField } from "@webapp/scripts/TypeFields/BaseTypeField";

// TODO: Create custom FormHandler implementation to handle typed form elements

// TODO expand and move to separate file
export class TypeFieldStore {
    static #store: Map<string, ITypeField<any>> = new Map();
    
    static set(key: string, field: ITypeField<any>): void {
        TypeFieldStore.#store.set(key, field);
    }
    static get<T>(id: string): ITypeField<T> | undefined {
        return TypeFieldStore.#store.get(id);
    }
    static has(id: string): boolean {
        return TypeFieldStore.#store.has(id);
    }
    static delete(...ids: string[]): void {
        for(const id of ids) TypeFieldStore.#store.delete(id);
    }
    static clear(): void {
        TypeFieldStore.#store.clear();
    }
}


export default abstract class AbstractFieldElement<T, TField extends ITypeField<T>> extends HTMLElement {
    static readonly formAssociated = true;
    static observedAttributes = ["disabled", "field-id"];

    /** 
     * The html tag name of this field. Subclasses should override this!
     * @abstract
     */
    static tagName: string;

    #abortController?: AbortController;
    protected mainFocus?: HTMLElement;
    /** The underlying value of the element. */
    protected _value?: T;

    #typeField?: TField;    // hold strong ref for the lifetime of the element
    protected readonly internals: ElementInternals;
     
    constructor() {
        super();
        this.internals = this.attachInternals();
    }

    

    get typeField(): TField {
        if(!this.#typeField) throw new Error("Cannot access typeField before it has been set.");
        return this.#typeField;
    }

    get name(): string | null { 
        return this.getAttribute("name"); 
    }

    set name(value: string) { 
        this.setAttribute("name", value); 
    }

    get value(): T | undefined {
        return this._getValue();
    }

    set value(value: T) {
        this._setValue(value);
        this.dispatchEvent(new Event("input", { bubbles: true, cancelable: true }));
        this.dispatchEvent(new Event("change", { bubbles: true, cancelable: true }));
        this._refresh();
    }

    get disabled() { 
        return this.matches(":disabled"); 
    }

    set disabled(value: boolean) { 
        this.toggleAttribute("disabled", value);
    }

    get abortSignal(): AbortSignal | undefined {
        return this.#abortController?.signal;
    }

    get form(): HTMLFormElement | null {
        return this.internals.form;
    }

    get editable(): boolean {
        return !this.disabled && !this.hasAttribute("readonly");
    }

    /** 
     * Lifecycle method. Should not be overridden.
     * @final
     */
    readonly connectedCallback = () => {
        this.#abortController = new AbortController();
        this.classList.add("form-field");
        const elements = this._buildElements();
        this.replaceChildren(...elements);
        this._refresh();
        this.addEventListener("click", this.#onClick, { signal: this.abortSignal });
        this._activateListeners();
    }

    /** 
     * Lifecycle method. Should not be overridden.
     * @final
     */
    readonly disconnectedCallback = () => {
        this.#abortController?.abort();
        this._onDisconnect();
    }

    /** 
     * Lifecycle method. Should not be overridden.
     * @final
     */
    readonly attributeChangedCallback = (attrName: string, oldValue: string, newValue: string): void => {
        if(attrName === "field-id" && oldValue !== newValue) {
            if(!newValue) throw new Error("Misconfiguration: FormElement must have a corresponding field id.");

            const field = TypeFieldStore.get<T>(newValue);
            if(!field) throw new Error(`Misconfiguration: Unable to find field id "${newValue}" in datastore.`);

            if(!this.fieldIsValidInstance(field)) throw new Error("Field is not a valid field.");
            this.#typeField = field;
        }

        this._onAttributeChanged(attrName, oldValue, newValue);
    }

    formDisabledCallback(disabled: boolean): void {
        if(this.isConnected) this._toggleDisabled(disabled);
    }

    /** Override to implement custom behavior in response to attribute changes.  */
    protected _onAttributeChanged(attrName: string, oldValue: string, newValue: string): void {};
    /** Type guard to validate that a given field is a valid field expected by the subclass. */
    protected abstract fieldIsValidInstance(field: ITypeField<T>): field is TField;
    /** Create the children of this element. */
    protected abstract  _buildElements(): HTMLElement[];
    /** Refresh the state of the element. */
    protected abstract _refresh(): void;
    /** Custom behaviour when toggling the distabled attribute on the element. */
    protected _toggleDisabled(disabled: boolean): void {};
    /** Override to activate any event listeners on the element or its children. */
    protected _activateListeners(): void {}
    /** Override to cleanup when the element is disconnected from the DOM. */
    protected _onDisconnect(): void {}
    /** Get the value of element that should be submitted to the form. */
    protected _getValue(): T | undefined {
        return this._value;
    }
    /** Cast the value and set it as the value of the element. */
    protected _setValue(value: T): void {
        const val = this.typeField.cast(value);
        if(val === null) {
            const validationFailure = this.typeField.validate(value);
            if(validationFailure) throw validationFailure.asError();
        }
        this._value = val!;
    }

    /** Sync "disabled", "readonly", and "required" attributes of the element with an inner element. */
    protected syncAttributesHelper(innerElement: HTMLElement): void {
        innerElement.toggleAttribute("disabled", this.hasAttribute("disabled"));
        innerElement.toggleAttribute("readonly", this.hasAttribute("readonly"));
        innerElement.toggleAttribute("required", this.hasAttribute("required"));
    }

    /** Redirect focus to the main internal element if possible. */
    #onClick = (event: PointerEvent): void => {
        if(event.target === this) this.mainFocus?.focus();
    }
}
