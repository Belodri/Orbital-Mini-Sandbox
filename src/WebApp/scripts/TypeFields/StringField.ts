import BaseTypeField from "./BaseTypeField";
import ValidationFailure from "./ValidationFailure";

export type StringFieldOptions = {
    /** Is the string allowed to be empty? Default = false */
    blank: boolean;
    /** A Set of values which represent allowed choices or a Map of choice values to corresponding string labels. */
    choices?: Set<string> | Map<string, string>;
    /** The minimum lenght the string must have. */
    minLength: number;
    /** The maximum length the string must have. */
    maxLength: number;
    /** Must the string be well formed and have no unpaired or unordered leading or trailing surrogates? Default = false */
    wellFormed: boolean;
    /** Should null be cast to `null`? Default = false */
    castNullAsString: boolean;
    /** Should undefined be cast to `undefined`? Default = false */
    castUndefinedAsString: boolean;
}

export const DEFAULT_OPTIONS: StringFieldOptions = {
    blank: false,
    choices: undefined,
    minLength: 0,
    maxLength: Number.MAX_SAFE_INTEGER,
    wellFormed: false,
    castNullAsString: false,
    castUndefinedAsString: false,
} as const;

export default class StringField extends BaseTypeField<string, StringFieldOptions> {
    constructor(options: Partial<StringFieldOptions> = {}) {
        const opts = { ...DEFAULT_OPTIONS, ...options };

        if(__DEBUG__) {
            const { minLength, maxLength } = opts;
            if(minLength > maxLength) throw new Error("MinLength must be smaller than or equal to maxLength.");
            if(maxLength < 0) throw new Error("MaxLength must not be negative.");
        }


        super(opts);
    }

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