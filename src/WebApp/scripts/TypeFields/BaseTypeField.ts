import ValidationFailure, { ValidationError } from "./ValidationFailure";

export interface ITypeField<T extends unknown, TOptions extends Record<string, any> = Record<string, any>> {
    /** The options with which this TypeField was instantiated. */
    readonly options: Readonly<TOptions>;
    /** The options to which the TypeField defaults to. */
    getDefaultOptions(): Readonly<TOptions>
    /** The value of the field. */
    get value(): T;
    /**
     * Tries to set a given value into the the field's type.
     * @param value The value to set. Will be validated again.
     * @returns The newly set and valid value.
     * @throws A {@link ValidationError} if the value is invalid. 
     */
    set(value: T): T;
    /**
     * Tries to cast a given value into the the field's type.
     * @param value The value to cast into the field's type.
     * @returns The correctly typed and validated value or `null`.
     */
    cast(value: any): T | null;
    /**
     * Validates potential input for this field.
     * @param value The value to be tested.
     * @returns `void` if valid, otherwise {@link ValidationFailure} that includes the value and the reason it failed
     */
    validate(value: any): void | ValidationFailure;
    /**
     * Type guard that verifies that a potential input for this field is valid.
     * @param value The value to be tested.
     * @returns `true` if valid and of the expected type.
     */
    isValid(value: any): value is T;
}

export default abstract class BaseTypeField<
    T extends unknown, 
    TOptions extends Record<string, any> = Record<string, any>
> implements ITypeField<T, TOptions> {
    protected _value: T;
    readonly options: Readonly<TOptions>;
    
    constructor(initialValue: T, options: TOptions = {} as TOptions) {
        this.options = Object.freeze({
            ...this.getDefaultOptions(),
            ...options
        });

        const validationFailure = this.validate(initialValue);
        if(validationFailure) throw validationFailure.asError();

        this._value = initialValue;
    }

    get value(): T { return this._value; }

    set(value: T): T {
        const validationFailure = this.validate(value);
        if(validationFailure) throw validationFailure.asError();

        this._value = value;
        return this._value;
    }

    cast(value: any): T | null {
        return this.isValid(value) ? value : null;
    }

    isValid(value: any): value is T {
        return this.validate(value) === undefined;
    }

    abstract validate(value: any): void | ValidationFailure;
    abstract getDefaultOptions(): Readonly<TOptions>;
}