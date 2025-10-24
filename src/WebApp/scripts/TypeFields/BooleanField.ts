import BaseTypeField from "./BaseTypeField";
import ValidationFailure from "./ValidationFailure";

export type BooleanFieldOptions = {
    /** Any value within this set is considered `true` for casting. Must be disjoint from {@link castAsFalse}. */
    castAsTrue?: Set<any>;
    /** Any value within this set is considered `false` for casting. Must be disjoint from {@link castAsTrue}. */
    castAsFalse?: Set<any>;
}

export const DEFAULT_OPTIONS: BooleanFieldOptions = {
    castAsTrue: undefined,
    castAsFalse: undefined,
} as const;

export default class BooleanField extends BaseTypeField<boolean, BooleanFieldOptions> {
    constructor(initialValue: boolean, options: BooleanFieldOptions = {}) {
        const { castAsTrue, castAsFalse } = options;
        if(castAsFalse && !castAsTrue?.isDisjointFrom(castAsFalse)) throw new Error(`The castAsTrue and castAsFalse Sets are not disjoint.`);

        super(initialValue, options);
    }

    override getDefaultOptions(): Readonly<BooleanFieldOptions> { return DEFAULT_OPTIONS; }

    override cast(value: any): boolean | null {
        const { castAsTrue, castAsFalse } = this.options;

        const bool = castAsTrue?.has(value) ? true 
            : castAsFalse?.has(value) ? false
            : value;
    
        return super.cast(bool);
    }

    validate(value: any): void | ValidationFailure {
        if(typeof value !== "boolean") return new ValidationFailure(value, "Must be a boolean.");
    }
}