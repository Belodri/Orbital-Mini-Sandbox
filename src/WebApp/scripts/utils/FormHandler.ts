/** Record of input field keys to values. */
export type FormDataSchema = Record<string, ValidValueType>;

type ValidValueType = string | number | boolean;

//#region Converters

type InputTypeConverters<T extends ValidValueType> = Record<string, { 
    read: (input: HTMLInputElement) => T,
    write: (input: HTMLInputElement, value: T) => void
}>;

const DEFAULT_STRING_CONVERTER = {
    read: (input: HTMLInputElement) => input.value,
    write: (input: HTMLInputElement, value: string) => { input.value = value }
} as const;

const DEFAULT_NUMBER_CONVERTER = {
    read: (input: HTMLInputElement) => input.valueAsNumber,
    write: (input: HTMLInputElement, value: number) => { input.valueAsNumber = value }
} as const;

const BOOLEAN_CONVERTERS: InputTypeConverters<boolean> = {
    "checkbox": {
        read: (input: HTMLInputElement) => input.checked,
        write: (input: HTMLInputElement, value: boolean) => { input.checked = value }
    },
} as const;

const STRING_CONVERTERS: InputTypeConverters<string> = {
    "color": DEFAULT_STRING_CONVERTER,
    "text": DEFAULT_STRING_CONVERTER,
} as const;

const NUMBER_CONVERTERS: InputTypeConverters<number> = {
    "number": DEFAULT_NUMBER_CONVERTER,
    "range": DEFAULT_NUMBER_CONVERTER,
} as const;

const CONVERTER_RECORD = {
    boolean: BOOLEAN_CONVERTERS,
    string: STRING_CONVERTERS,
    number: NUMBER_CONVERTERS
} as const;

//#endregion


class InputContext<T extends FormDataSchema, K extends keyof T & string> {
    readonly input: HTMLInputElement;
    readonly key: K;
    readonly read: () => T[K];
    readonly write: (value: T[K]) => void;
    readonly customValidator?: (input: HTMLInputElement) => boolean;

    constructor(input: HTMLInputElement, key: K, data: T, customValidator?: (input: HTMLInputElement) => boolean) {
        this.input = input;
        this.key = key;
        const valueType = typeof data[key];

        if( !(valueType in CONVERTER_RECORD) ) throw new Error(`Invalid value type "${valueType}" in data on input with name "${key}".`);
        const converters = CONVERTER_RECORD[valueType as keyof typeof CONVERTER_RECORD];

        if( !(input.type in converters) ) throw new Error(`Unsupported input type "${input.type}" for data type "${valueType}" on input with name "${key}".`)
        const converter = converters[input.type];

        this.read = () => converter.read(this.input) as T[K];
        this.write = (value: T[K]) => (converter.write as (input: HTMLInputElement, value: T[K]) => void)(this.input, value);
        if(customValidator) this.customValidator = customValidator;
    }

    validate = () => this.input.checkValidity() && ( this.customValidator?.(this.input) ?? true );
}

export type CustomValidators<T extends FormDataSchema> = Partial<Record<keyof T, (input: HTMLInputElement) => boolean>>;

export interface IFormHandler<T extends FormDataSchema> {
    /**
     * Updates the `<input>` fields of the `<form>` managed by this handler on the live DOM. 
     * @param data Partial update data.
     */
    setData(data: Partial<T>): void;
}

export default class FormHandler<T extends FormDataSchema> implements IFormHandler<T> {
    readonly #form: HTMLFormElement;
    readonly #onValidSubmit: (data: T) => void;
    readonly #baseData: T;
    readonly #inputs: Map<keyof T, InputContext<T, keyof T & string>> = new Map();

    constructor(form: HTMLFormElement, data: T, onValidSubmit: (data: T) => void, customValidators?: CustomValidators<T>) {
        this.#form = form;
        this.#onValidSubmit = onValidSubmit;
        this.#baseData = { ...data };

        for(const key of Object.keys(data) as (keyof T & string)[]) {
            const input = this.#form.querySelector(`input[name='${key}']`);
            if(!(input instanceof HTMLInputElement)) throw new Error(`Schema mismatch. Unable to find input field for key "${key}".`);

            this.#inputs.set(key, new InputContext( input, key, data, customValidators?.[key] ));
        }

        this.setData(data);
        this.#registerEventListeners();
    }

    #registerEventListeners(): void {
        this.#form.addEventListener("submit", this.#onSubmit.bind(this));

        for(const ctx of this.#inputs.values()) {
            ctx.input.addEventListener("blur", () => this.#validateInputValue(ctx));
            ctx.input.addEventListener("input", () => {
                if(ctx.input.classList.contains("invalid")) this.#validateInputValue(ctx);
            });
        }
    }

    #onSubmit(event: SubmitEvent) {
        event.preventDefault();

        let allValid = true;
        for(const ctx of this.#inputs.values()) {
            if(!this.#validateInputValue(ctx)) allValid = false;
        }

        if(allValid) {
            const data = this.#getData();
            this.#onValidSubmit(data);
        }
    }

    #validateInputValue(ctx: InputContext<T, keyof T & string>): boolean {
        const { input } = ctx;
        const isValid = ctx.validate();

        input.classList.toggle("invalid", !isValid);
        input.setAttribute("aria-invalid", String(!isValid));

        return isValid;
    }

    setData(data: Partial<T>): void {
        for(const [key, value] of Object.entries(data)) {
            const ctx = this.#inputs.get(key);
            if(ctx && value !== undefined) ctx.write(value);
        }
    }

    #getData(): T {
        const data = { ...this.#baseData };
        for(const [key, { read }] of this.#inputs) {
            data[key] = read();
        }
        return data;
    }
}