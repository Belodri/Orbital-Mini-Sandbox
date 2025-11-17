import BaseTypeField from "./BaseTypeField";
import ValidationFailure from "./ValidationFailure";

export type NumberFieldOptions = {
    /** The maximum allowed value. */
    min?: number;
    /** The maximum allowed value. */
    max?: number;
    /** The number the field value must be a multiple of. Step base is {@link min} if specified, or 0 otherwise. */
    step?: number;
    /** Must the field value be a safe integer? */
    safeInteger?: boolean;
    /** A Set of values which represent allowed choices. */
    choices?: Set<number>;
}

const DEFAULT_OPTIONS: NumberFieldOptions = {
    min: undefined,
    max: undefined,
    step: undefined,
    safeInteger: false,
    choices: undefined,
} as const;

export default class NumberField extends BaseTypeField<number, NumberFieldOptions> {
    constructor(options: NumberFieldOptions = {}) {
        if(__DEBUG__) {
            const { min, max } = options;
            if(typeof min === "number" && typeof max === "number" && min >= max) throw new Error("If min and max are both given, min must be smaller than max.");
        }

        super(options);
    }

    override _getDefaultOptions(): Readonly<NumberFieldOptions> { return DEFAULT_OPTIONS; }

    override cast(value: any): number | null {
        if(typeof value === "bigint" || typeof value === "symbol") return null;

        return super.cast(Number(value));
    }

    override validate(value: any): void | ValidationFailure {
        if(typeof value !== "number" || Number.isNaN(value)) return new ValidationFailure(value, "Must be a number.");

        const { min, max, step, safeInteger, choices } = this.options;

        if(min !== undefined && value < min) return new ValidationFailure(value, `Must be greater than or equal to ${min}.`);
        if(max !== undefined && value > max) return new ValidationFailure(value, `Must be smaller than or equal to ${max}.`);
        if(step !== undefined) {
            const remainder = ( value - ( min ?? 0 ) ) / step;
            const floatDiff = Math.abs(remainder - Math.round(remainder));
            if(floatDiff > Number.EPSILON) return new ValidationFailure(value, `Must be a multiple of ${step}` + (min !== undefined ? `from ${min}.` : "."));
        }
        if(safeInteger && !Number.isSafeInteger(value)) return new ValidationFailure(value, `Must be a safe integer.`);
        if(choices && !choices.has(value)) return new ValidationFailure(value, "Must be a valid choice.");
    }
}