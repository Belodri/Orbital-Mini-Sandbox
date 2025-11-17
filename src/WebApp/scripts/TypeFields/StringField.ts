import BaseTypeField from "./BaseTypeField";
import ValidationFailure from "./ValidationFailure";

export type StringFieldOptions = {
    /** Is the string allowed to be empty? (default=false) */
    blank?: boolean;
    /** A Set of values which represent allowed choices. */
    choices?: Set<string>;
    /** The minimum lenght the string must have. */
    minLength?: number;
    /** The maximum length the string must have. */
    maxLength?: number;
    /** Must the string be well formed and have no unpaired or unordered leading or trailing surrogates? */
    wellFormed?: boolean;
    /** Should null be cast to `null`? */
    castNullAsString?: boolean;
    /** Should undefined be cast to `undefined`? */
    castUndefinedAsString?: boolean;
}

export const DEFAULT_OPTIONS: StringFieldOptions = {
    blank: false,
    choices: undefined,
    minLength: undefined,
    maxLength: undefined,
    wellFormed: false,
    castNullAsString: false,
    castUndefinedAsString: false,
} as const;

export default class StringField extends BaseTypeField<string, StringFieldOptions> {
    constructor(options: StringFieldOptions = {}) {
        if(__DEBUG__) {
            const { minLength, maxLength } = options;
            if(typeof minLength === "number" && typeof maxLength === "number" && minLength >= maxLength) 
                throw new Error("If minLength and maxLength are both given, minLength must be smaller than maxLength.");
            if( typeof maxLength === "number" && maxLength < 0) throw new Error("If maxLength is given, it must not be negative.");
        }
        super(options);
    }

    override _getDefaultOptions(): Readonly<StringFieldOptions> { return DEFAULT_OPTIONS; }

    override cast(value: any): string | null {
        const { castNullAsString, castUndefinedAsString } = this.options;

        if(typeof value === "symbol") return null;
        if(!castNullAsString && value === null) return null;
        if(!castUndefinedAsString && value === undefined) return null;

        return super.cast(String(value));
    }

    override validate(value: any): void | ValidationFailure {
        if(typeof value !== "string") return new ValidationFailure(value, "Must be a string.");

        const { blank, minLength, maxLength, choices, wellFormed } = this.options;

        if(!blank && !value.trim().length) return new ValidationFailure(value, "Must not be blank.");
        if(minLength !== undefined && value.length < minLength) return new ValidationFailure(value, `Must be at least ${minLength} characters long.`);
        if(maxLength !== undefined && value.length > maxLength) return new ValidationFailure(value, `Must be no more than ${maxLength} characters long.`);
        if(wellFormed && !value.isWellFormed()) return new ValidationFailure(value, `Must be well formed without any unpaired or unordered leading or trailing surrogates.`);
        if(choices && !choices.has(value)) return new ValidationFailure(value, "Must be a valid choice.");
    }
}