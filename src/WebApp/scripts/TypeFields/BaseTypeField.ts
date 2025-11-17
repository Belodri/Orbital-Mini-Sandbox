import ValidationFailure from "./ValidationFailure";

export interface ITypeField<T extends unknown, TOptions extends Record<string, any> = Record<string, any>> {
    /** The options with which this TypeField was instantiated. */
    readonly options: Readonly<TOptions>;
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
    readonly options: Readonly<TOptions>;
    
    constructor(options: TOptions = {} as TOptions) {
        const defaultOptions = this._getDefaultOptions?.() ?? {};
        this.options = Object.freeze({
            ...defaultOptions,
            ...options
        });
    }
 
    cast(value: any): T | null {
        return this.isValid(value) ? value : null;
    }

    isValid(value: any): value is T {
        return this.validate(value) === undefined;
    }

    abstract validate(value: any): void | ValidationFailure;

    /** The options to which the TypeField defaults to. */
    protected _getDefaultOptions?(): Readonly<TOptions>;
}