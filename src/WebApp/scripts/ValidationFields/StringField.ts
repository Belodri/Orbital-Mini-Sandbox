import AbstractValidationField from "./AbstractValidationField";
import ValidationFailure from "./ValidationFailure";

export type StringFieldOptions = {
    /** Is the string allowed to be empty? Default = false */
    blank: boolean;
    /** A Set of values which represent allowed choices or a Map of choice values to corresponding string labels. */
    choices?: Set<string> | Map<string, string>;
    /** The minimum length the string must have. */
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

export default class StringField extends AbstractValidationField<string, StringFieldOptions> {
    protected override prepareOptions(partialOptions: Partial<StringFieldOptions>): StringFieldOptions {
        return { ...DEFAULT_OPTIONS, ...partialOptions };
    }
    protected override validateOptions(options: Readonly<StringFieldOptions>): void {
        const { minLength, maxLength, choices, blank, wellFormed } = options;
        if(minLength > maxLength) throw new Error("MinLength must be smaller than or equal to maxLength.");
        if(maxLength < 0) throw new Error("MaxLength must not be negative.");
        
        if(choices) {
            for(const choice of choices.keys()) {
                if(!blank && choice.trim().length === 0) throw new Error("Blank values are not allowed as choices.");
                if(choice.length < minLength) throw new Error("Choices must be longer than minLength.");
                if(choice.length > maxLength) throw new Error("Choices must be greater than maxLength");
                if(wellFormed && !choice.isWellFormed()) throw new Error("Choices must be well formed.");
            }
        }
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
        if(value.length < minLength) return new ValidationFailure(value, `Must be at least ${minLength} characters long.`);
        if(value.length > maxLength) return new ValidationFailure(value, `Must be no more than ${maxLength} characters long.`);
        if(wellFormed && !value.isWellFormed()) return new ValidationFailure(value, `Must be well formed without any unpaired or unordered leading or trailing surrogates.`);
        if(choices && !choices.has(value)) return new ValidationFailure(value, "Must be a valid choice.");
    }
}