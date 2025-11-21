import ValidationFailure from "./ValidationFailure";

export interface ITypeField<T extends unknown, TOptions extends Record<string, any> = Record<string, any>> {
    /** The options with which this TypeField was instantiated. Frozen upon instantiation and should never be mutated! */
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

export default abstract class BaseTypeField<T, TOptions extends Record<string, any> = Record<string, any>> implements ITypeField<T, TOptions> {
    readonly options: Readonly<TOptions>;
    
    constructor(options: Partial<TOptions> = {}) {
        const opts = this.prepareOptions({...options});
        const frozen = Object.freeze(opts);

        this.validateOptions(frozen);
        this.options = frozen;
    }

    /**
     * Prepares the options object for the type field during the field's instatiation.
     * @param partialOptions A shallow copy of the partial options object.
     * @returns A fully constructed options object.
     */
    protected abstract prepareOptions(partialOptions: Partial<TOptions>): TOptions;
    /**
     * Subclasses should override this to validate the fully prepared and frozen options object during the field's instatiation.
     * @throws If any option is invalid.
     */
    protected validateOptions(options: Readonly<TOptions>): void {};
 
    cast(value: any): T | null {
        return this.isValid(value) ? value : null;
    }

    isValid(value: any): value is T {
        return this.validate(value) === undefined;
    }

    abstract validate(value: any): void | ValidationFailure;
}