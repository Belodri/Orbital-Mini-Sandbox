import ValidationFailure from "./ValidationFailure";

// Abstract class instead of interface to ensure runtime identity checks are possible.
export default abstract class ValidationField<T, TOptions extends Record<string, any> = Record<string, any>> {
    /** Nominal typing guard. Prevents a class from satisfying `implements ValidationField` unless the class actually extends this class. */
    #implementationGuard: undefined;
    /** The options with which this ValidationField was instantiated. */
    abstract readonly options: Readonly<TOptions>;
    /**
     * Tries to cast a given value into the the field's type.
     * @param value The value to cast into the field's type.
     * @returns The correctly typed and validated value or `null`.
     */
    abstract cast(value: any): T | null;
    /**
     * Validates potential input for this field.
     * @param value The value to be tested.
     * @returns `void` if valid, otherwise {@link ValidationFailure} that includes the value and the reason it failed
     */
    abstract validate(value: any): void | ValidationFailure;
    /**
     * Type guard that verifies that a potential input for this field is valid.
     * @param value The value to be tested.
     * @returns `true` if valid and of the expected type.
     */
    abstract isValid(value: any): value is T;
}
