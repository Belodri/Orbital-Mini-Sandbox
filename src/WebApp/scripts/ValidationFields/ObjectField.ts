import BaseValidationField, { type BaseValidationFieldOptions } from "./BaseValidationField";
import ValidationField from "./ValidationField";
import ValidationFailure from "./ValidationFailure";

export type FieldsSchema = Record<string, ValidationField<any>>;

export type InferredType<T extends FieldsSchema> = {
    [K in keyof T]: T[K] extends ValidationField<infer U> ? U : never;
};

export type ObjectFieldOptions = BaseValidationFieldOptions & {};
const DEFAULT_OPTIONS: ObjectFieldOptions = {} as const;

export default class ObjectField<
    TSchema extends FieldsSchema, 
    T extends InferredType<TSchema> = InferredType<TSchema>
> extends BaseValidationField<T, ObjectFieldOptions> {
    readonly #schemaKeys: ReadonlySet<keyof TSchema>;
    readonly #schema: Readonly<TSchema>;

    /**
     * @param schema The {@link FieldsSchema} Record of fields contained within this ObjectField, which can include other ObjectFields.
     */
    constructor(schema: TSchema, options: Partial<ObjectFieldOptions> = {}) {
        const schemaKeys = Object.keys(schema ?? {});
        if(__DEBUG__ && !schemaKeys.length) throw new Error("Must provide a valid, non-empty schema.");

        super(options);
        this.#schema = Object.freeze({...schema});
        this.#schemaKeys = new Set(schemaKeys);
    }

    protected override prepareOptions(partialOptions: Partial<ObjectFieldOptions>): ObjectFieldOptions {
        return { ...DEFAULT_OPTIONS, ...partialOptions };
    }

    get schema(): Readonly<TSchema> { return this.#schema; }

    /**
     * Extra properties are stripped. The argument itself is not mutated.
     * @inheritdoc
     */
    override cast(value: any): T | null {
        if(typeof value === "string") {
            try {
                value = JSON.parse(value);
            } catch(err) {
                return null;
            }
        }

        if(typeof value !== 'object' || !value || Array.isArray(value)) return null;

        const newObj = {} as T;
        for(const key of this.#schemaKeys) {
            const field = this.schema[key]; 
            const castValue = field.cast(value[key]);   // Let the field decide how to handle missing properties.

            if(castValue === null) return null;
            newObj[key] = castValue;
        }

        return newObj;
    }

    /**
     * Any mismatch between value and schema will cause a {@link ValidationFailure}.  
     * Fails fast and returns a {@link ValidationFailure} on the first invalid property. 
     * @inheritdoc
     */
    override validate(value: any): void | ValidationFailure {  // If aggregating failures is needed, refactor ValidationFailure to allow it without altering call signatures.
        if(typeof value !== 'object' || !value || Array.isArray(value))
            return new ValidationFailure(value, "Must be an object.");

        for(const key of this.#schemaKeys) {
            const field = this.schema[key];     
            const validationFailure = field.validate(value[key]);
            if(validationFailure) return new ValidationFailure(value, `'${String(key)}': ${validationFailure.reason}`);
        }

        for(const key of Object.keys(value)) {
            if(!this.#schemaKeys.has(key)) return new ValidationFailure(value, "Extra properties are not permitted.");
        }
    }

    override valueToStringUnsafe(value: T): string {
        const toStringify: Record<string, string> = {};

        for(const key of this.#schemaKeys) {
            const field = this.schema[key];
            toStringify[key as string] = field.valueToStringUnsafe(value[key]); 
        }

        return JSON.stringify(toStringify);
    }
}