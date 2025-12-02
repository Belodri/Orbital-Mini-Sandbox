import BaseValidationField, { type BaseValidationFieldOptions} from "./BaseValidationField";
import ValidationFailure from "./ValidationFailure";

export type BooleanFieldOptions = BaseValidationFieldOptions & {
    /** 
     * Any value within this set is considered `true` for casting. Must be disjoint from {@link castAsFalse}. Defaults to `["on", "true"]`  
     * If the Set contains no string value, the field's {@link valueToString} and {@link valueToStringUnsafe} will throw an error when called with a `true` value. 
     */
    castAsTrue: Set<any>;
    /** 
     * Any value within this set is considered `false` for casting. Must be disjoint from {@link castAsTrue}. Defaults to `["", "false", undefined, null, 0, -0]`  
     * If the Set contains no string value, the field's {@link valueToString} and {@link valueToStringUnsafe} will throw an error when called with a `false` value. 
     */
    castAsFalse: Set<any>;
    /** 
     * An item from the {@link castAsTrue} Set that `true` values will be cast to by the field's {@link valueToString} and {@link valueToStringUnsafe} methods.
     * If not a string, the first string value from the {@link castAsTrue} Set will be used instead.
     */
    defaultTrueString: string | undefined;
    /** 
     * An item from the {@link castAsFalse} Set that `false` values will be cast to by the field's {@link valueToString} and {@link valueToStringUnsafe} methods.
     * If not a string, the first string value from the {@link castAsFalse} Set will be used instead.
     */
    defaultFalseString: string | undefined;
}

export const DEFAULT_OPTIONS: BooleanFieldOptions = {
    castAsTrue: new Set(["on", "true"]),
    castAsFalse: new Set(["", "false", undefined, null, 0, -0]),
    defaultTrueString: undefined,
    defaultFalseString: undefined
} as const;

export default class BooleanField extends BaseValidationField<boolean, BooleanFieldOptions> {
    protected override prepareOptions(partialOptions: Partial<BooleanFieldOptions>): BooleanFieldOptions {
        return { ...DEFAULT_OPTIONS, ...partialOptions }
    }

    protected override validateOptions(options: Readonly<BooleanFieldOptions>): void {
        const { castAsTrue, castAsFalse, defaultTrueString, defaultFalseString } = options;
        if(castAsFalse.size && castAsTrue.size && !castAsTrue.isDisjointFrom(castAsFalse))
            throw new Error(`The castAsTrue and castAsFalse Sets must be disjoint.`);
        if(castAsTrue.has(false)) throw new Error("Boolean false cannot be cast as true.");
        if(castAsFalse.has(true)) throw new Error("Boolean true cannot be cast as false.");
        if(typeof defaultTrueString === "string" && !castAsTrue.has(defaultTrueString))
            throw new Error(`The defaultTrueString "${defaultTrueString}" must be in the castAsTrue Set.`);
        if(typeof defaultFalseString === "string" && !castAsFalse.has(defaultFalseString))
            throw new Error(`The defaultFalseString "${defaultFalseString}" must be in the castAsFalse Set.`);
    }

    override cast(value: any): boolean | null {
        if(typeof value === "boolean") return value;

        const { castAsTrue, castAsFalse } = this.options;

        return castAsTrue.has(value) ? true
            : castAsFalse.has(value) ? false
            : null;
    }

    override validate(value: any): void | ValidationFailure {
        if(typeof value !== "boolean") return new ValidationFailure(value, "Must be a boolean.");
    }

    /**
     * @param stringRepresentations Optional specification for which key of {@link castAsTrue} and {@link castAsFalse} to cast the value to.
     *                              Will throw an error if the given string is not in the respective Set. 
     * 
     * @throws {Error} If no string representation of the value is found in the field's options.
     * @inheritdoc
     */
    override valueToStringUnsafe(value: boolean, stringRepresentations?: { trueString: string, falseString: string }): string {
        const { castAsTrue, castAsFalse, defaultTrueString, defaultFalseString } = this.options;

        if(stringRepresentations) {
            const castStr = value ? stringRepresentations.trueString : stringRepresentations.falseString;
            if((value ? castAsTrue : castAsFalse).has(castStr)) return castStr;

            throw new Error(`Optional ${value ? "trueString" : "falseString"} "${castStr}" is not a valid string representation for a ${String(value)} value.`);
        }

        const castStr = (value ? defaultTrueString : defaultFalseString)
            ?? (value ? castAsTrue : castAsFalse).values().find(v => typeof v === "string");
        
        if(typeof castStr === "string") return castStr;

        throw new Error(`The field has no string representation for a ${String(value)} value.`);
    }
}