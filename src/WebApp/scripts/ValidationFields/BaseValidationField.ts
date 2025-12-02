import ValidationField, { type ValidationFieldOptions } from "./ValidationField";

export type BaseValidationFieldOptions = ValidationFieldOptions;

export default abstract class BaseValidationField<T, TOptions extends BaseValidationFieldOptions = BaseValidationFieldOptions> extends ValidationField<T, TOptions> {
    readonly options: Readonly<TOptions>;
    
    constructor(options: Partial<TOptions> = {}) {
        super();
        const opts = this.prepareOptions({...options});
        const frozen = Object.freeze(opts);

        this.validateOptions(frozen);
        this.options = frozen;
    }

    /**
     * Prepares the options object for the ValidationField during the field's instatiation.
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
}