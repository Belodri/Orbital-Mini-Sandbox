import { ITypeField } from "@webapp/scripts/TypeFields/BaseTypeField";
import AbstractFieldElement from "./AbstractFieldElement";
import NumberField from "@webapp/scripts/TypeFields/NumberField";

class NumberFieldElement extends AbstractFieldElement<number, NumberField> {
    static override tagName: string = "number-field";

    #numberInput?: HTMLInputElement;
    #rangeInput?: HTMLInputElement;
    #selectInput?: HTMLSelectElement;

    constructor(value?: number) {
        super();
        const val = this.typeField.cast(value ?? this.getAttribute("value"));
        if (this.typeField.isValid(val)) this._value = val;
    }

    get valueAsNumber(): number {
        return this._getValue() ?? NaN;
    }

    protected override fieldIsValidInstance(field: ITypeField<number, Record<string, any>>): field is NumberField {
        return field instanceof NumberField;
    }

    protected override _buildElements(): HTMLElement[] {
        const { choices, min, max, step } = this.typeField.options;

        if (choices && choices.size > 1) {
            this.#selectInput = this.mainFocus = this.#createSelectElement(choices);
            this.syncAttributesHelper(this.#selectInput);
            return [this.#selectInput];
        }

        const elements = [];
        if (max - min < 500) {
            this.#rangeInput = this.#createInputElement("range", min, max, step);
            this.syncAttributesHelper(this.#rangeInput);
            elements.push(this.#rangeInput);
        }

        this.#numberInput = this.mainFocus = this.#createInputElement("number", min, max, step);
        this.syncAttributesHelper(this.#numberInput);
        elements.push(this.#numberInput);

        return elements;
    }

    #createInputElement(type: "number" | "range", min: number, max: number, step: number): HTMLInputElement {
        const ele = document.createElement("input");
        ele.type = type;

        if (min) ele.min = String(min);
        if (max) ele.max = String(max);
        ele.step = step ? String(step) : "any";

        return ele;
    }

    #createSelectElement(choices: Set<number> | Map<number, string>): HTMLSelectElement {
        const ele = document.createElement("select");
        const curr = this.value;

        for (const [key, value] of choices.entries()) {
            const opt = document.createElement("option");
            const keyStr = String(key);
            opt.label = typeof value === "string" ? value : keyStr;
            opt.value = keyStr;
            opt.selected = key === curr;
            ele.options.add(opt);
        }

        return ele;
    }

    protected override _refresh(): void {
        if (this.#numberInput) this.#numberInput.valueAsNumber = this.valueAsNumber;
        if (this.#rangeInput) this.#rangeInput.valueAsNumber = this.valueAsNumber;
        if (this.#selectInput) {
            const valStr = String(this._value ?? "");
            for (const opt of this.#selectInput.options) {
                opt.selected = opt.value === valStr;
            }
        }
    }

    protected override _activateListeners(): void {
        this.#rangeInput?.addEventListener("input", this.#onDragInputSlider);
        this.#rangeInput?.addEventListener("change", this.#onChangeInput);
        this.#numberInput?.addEventListener("change", this.#onChangeInput);
    }

    protected override _toggleDisabled(disabled: boolean): void {
        if (this.#numberInput) this.#numberInput.disabled = disabled;
        if (this.#rangeInput) this.#rangeInput.disabled = disabled;
        if (this.#selectInput) this.#selectInput.disabled = disabled;
    }

    #onChangeInput = (event: Event) => {
        event.stopPropagation();
        this.value = (event.currentTarget as HTMLInputElement).valueAsNumber;
    };

    #onDragInputSlider = (event: Event) => {
        event.preventDefault();
        // Safe because if range input exists, number input does as well.
        this.#numberInput!.valueAsNumber = this.#rangeInput!.valueAsNumber;
    };

    protected override _setValue(value: number): void {
    }
}

customElements.define(NumberFieldElement.tagName, NumberFieldElement);