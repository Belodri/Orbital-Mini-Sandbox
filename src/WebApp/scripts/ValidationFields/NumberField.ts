import BaseValidationField from "./AbstractValidationField";
import ValidationFailure from "./ValidationFailure";

/** Options to configure a {@link NumberField}. A value validated by this field is guaranteed to meet all given requirements. */
export type NumberFieldOptions = {
    /** The minimum allowed value. Default = {@link Number.MIN_SAFE_INTEGER}. */
    min: number;
    /** The maximum allowed value. Default = {@link Number.MAX_SAFE_INTEGER}. */
    max: number;
    /** The step size allowed by the field. Default = 0 */
    step: number;
    /** The base from which steps are calculated. Default = 0 */
    stepBase: number;
    /** Must the field value be an integer? Values cast to integers are rounded.  Default = false */
    integer: boolean;
    /** 
     * A Set of values which represent allowed choices or a Map of choice values to corresponding string labels.
     * For validation, only values that strictly equal a choice are considered valid!
     */
    choices?: Set<number> | Map<number, string>;
    /** Cast undefined values as 0? Default = false */
    castUndefinedAsZero: boolean;
    /** Cast null values as 0? Default = false */
    castNullAsZero: boolean;
    /** Cast blank string values as 0? Default = false */
    castBlankStringAsZero: boolean;
    /** Softening factor to handle IEEE 754 floating point issues. Default = 1e-12 */
    epsilon: number;
}

const DEFAULT_OPTIONS: NumberFieldOptions = {
    min: Number.MIN_SAFE_INTEGER,
    max: Number.MAX_SAFE_INTEGER,
    step: 0,
    stepBase: 0,
    integer: false,
    choices: undefined,
    castUndefinedAsZero: false,
    castNullAsZero: false,
    castBlankStringAsZero: false,
    epsilon: 1e-12
} as const;

export default class NumberField extends BaseValidationField<number, NumberFieldOptions> {
    protected override prepareOptions(partialOptions: Partial<NumberFieldOptions>): NumberFieldOptions {
        return { ...DEFAULT_OPTIONS, ...partialOptions };
    }

    protected override validateOptions(options: Readonly<NumberFieldOptions>): void {
        const { min, max, step, stepBase, choices, integer, epsilon } = options;

        for(const [key, value] of Object.entries(options)) {
            if(typeof value === "number" && Number.isNaN(value))
                throw new Error(`Numeric argument "${key}" cannot be NaN.`);
        }

        if(min > max) throw new Error("Min must be smaller than or equal to max.");
        if(step < 0) throw new Error("Step must not be negative.");

        if(integer) {
            if(!Number.isInteger(min)) throw new Error("Min must be an integer.");
            if(!Number.isInteger(max)) throw new Error("Max must be an integer.");
            if(step && !Number.isInteger(step)) throw new Error("Step must be an integer.");
            if(stepBase && !Number.isInteger(stepBase)) throw new Error("StepBase must be an integer.");
        }

        if(choices) {
            for(const choice of choices.keys()) {
                if(choice > max) throw new Error("Choices must be smaller than max.");
                if(choice < min) throw new Error("Choices must be greater than min.");
                if(step) {
                    const choiceSteps = (choice - stepBase) / step;
                    const approxEq = choiceSteps.approxEquals(Math.round(choiceSteps), epsilon);
                    if(!approxEq) throw new Error("Each Choice must be stepBase + a multiple of step.");
                }
                if(integer && !Number.isInteger(choice)) throw new Error("Choices must be safe integers.")
            }
        }
    }

    /**
     * 
     * Follows standard JS number coercion rules with exceptions specified in the options.  
     * [MDN Reference](https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Global_Objects/Number#number_coercion) 
     * @inheritdoc
     */
    override cast(value: any): number | null {
        let v = this.#castToNumber(value);
        if(v === null) return null;

        const { min, max, integer, choices } = this.options;

        if(choices) return this.#castChoice(v);

        v = this.#tryCastStep(v) ?? Math.clamp(v, min, max);

        if(integer) v = Math.round(v);

        if(__DEBUG__) {
            const validationFailure = this.validate(v);
            if(validationFailure) throw validationFailure.asError();
        }
        return v;
    }

    #castToNumber(value: any): number | null {
        if(typeof value === "symbol") return null;
        if(typeof value === "bigint") {
            const n = Number(value);
            return Number.isSafeInteger(n) ? n: null;
        }

        const { castUndefinedAsZero, castNullAsZero, castBlankStringAsZero } = this.options;
        if(value === undefined) return castUndefinedAsZero ? 0 : null; 
        if(value === null) return castNullAsZero ? 0 : null;
        if(typeof value === "string" && value.trim() === "")
            return castBlankStringAsZero ? 0 : null;

        const v = Number(value);
        return Number.isNaN(v) ? null : v;
    }

    #castChoice(value: number ): number | null {
        const { choices, epsilon, integer } = this.options;
        if(!choices?.size) return null;

        if(integer) {
            const round = Math.round(value);
            if(choices.has(round)) return round;
        }

        for(const key of choices.keys()) {
            if(value.approxEquals(key, epsilon)) return key;
        }

        return null;
    }

    #minSteps?: number;
    #maxSteps?: number;
    #tryCastStep(value: number): number | null {
        const { min, max, step, stepBase, epsilon } = this.options;
        if(!step) return null;

        const maxSteps = this.#maxSteps ??= Math.floor(((max - stepBase) / step) + epsilon);
        const minSteps = this.#minSteps ??= Math.ceil(((min - stepBase) / step) - epsilon);

        let steps = Math.round((value - stepBase) / step);
        steps = Math.clamp(steps, minSteps, maxSteps);

        return stepBase + steps * step;
    }

    override validate(value: any): void | ValidationFailure {
        if(typeof value !== "number" || Number.isNaN(value)) return new ValidationFailure(value, "Must be a number.");

        const { min, max, step, stepBase, integer, choices, epsilon } = this.options;

        if(value < min - epsilon) return new ValidationFailure(value, `Must be greater than or equal to ${min}.`);
        if(value > max + epsilon) return new ValidationFailure(value, `Must be smaller than or equal to ${max}.`);
        if(step) {
            const remainder = ( value - stepBase ) / step;
            const approxEq = remainder.approxEquals(Math.round(remainder), epsilon);
            if(!approxEq) return new ValidationFailure(value, `Must be a multiple of ${step} from ${stepBase}.`);
        }
        if(integer && !Number.isSafeInteger(value)) return new ValidationFailure(value, `Must be a safe integer.`);
        if(choices && !choices?.has(value)) return new ValidationFailure(value, "Must be a valid choice.");
    }
}