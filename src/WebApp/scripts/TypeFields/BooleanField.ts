import BaseTypeField from "./BaseTypeField";
import ValidationFailure from "./ValidationFailure";

export type BooleanFieldOptions = {
    /** Any value within this set is considered `true` for casting. Must be disjoint from {@link castAsFalse}. */
    castAsTrue?: Set<any>;
    /** Any value within this set is considered `false` for casting. Must be disjoint from {@link castAsTrue}. Defaults to `Set([undefined, null, "", 0, -0])` */
    castAsFalse?: Set<any>;
}

export const DEFAULT_OPTIONS: BooleanFieldOptions = {
    castAsTrue: undefined,
    castAsFalse: new Set([undefined, null, "", 0, -0]),
} as const;

export default class BooleanField extends BaseTypeField<boolean, BooleanFieldOptions> {
    protected override prepareOptions(partialOptions: Partial<BooleanFieldOptions>): BooleanFieldOptions {
        return { ...DEFAULT_OPTIONS, ...partialOptions }
    }
    protected override validateOptions(options: Readonly<BooleanFieldOptions>): void {
        const { castAsTrue, castAsFalse } = options;
        if(castAsFalse?.size && castAsTrue?.size && !castAsTrue.isDisjointFrom(castAsFalse))
            throw new Error(`The castAsTrue and castAsFalse Sets must be disjoint.`);
        if(castAsTrue?.has(false)) throw new Error("Boolean false cannot be cast as true.");
        if(castAsFalse?.has(true)) throw new Error("Boolean true cannot be cast as false.");
    }

    override cast(value: any): boolean | null {
        if(typeof value === "boolean") return value;

        const { castAsTrue, castAsFalse } = this.options;

        return castAsTrue?.has(value) ? true
            : castAsFalse?.has(value) ? false
            : null;
    }

    override validate(value: any): void | ValidationFailure {
        if(typeof value !== "boolean") return new ValidationFailure(value, "Must be a boolean.");
    }
}