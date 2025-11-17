import BaseTypeField, { ITypeField } from "./BaseTypeField";
import ValidationFailure from "./ValidationFailure";

export type FieldsSchema = Record<string, ITypeField<any>>;

export type InferredType<T extends FieldsSchema> = {
    [K in keyof T]: T[K] extends ITypeField<infer U> ? U : never;
};

export type ObjectFieldOptions<T extends FieldsSchema> = {
    /** The fields schema used to validate the object's properties. */
    schema: T;
}

export default class ObjectField<
    TSchema extends FieldsSchema, 
    T extends InferredType<TSchema> = InferredType<TSchema>
> extends BaseTypeField<T> {
    readonly #schemaKeys: ReadonlySet<keyof TSchema>;
    readonly #schema: Readonly<TSchema>;

    constructor(schema: TSchema, options: Record<string, any> = {}) {
        const schemaKeys = Object.keys(schema ?? {});
        if(__DEBUG__ && !schemaKeys.length) throw new Error("Must provide a valid, non-empty schema.");

        super(options);
        this.#schema = Object.freeze({...schema});
        this.#schemaKeys = new Set(schemaKeys);
    }

    get schema(): Readonly<TSchema> { return this.#schema; }

    /**
     * Extra properties are stripped. The argument itself is not mutated.
     * @inheritdoc
     */
    override cast(value: any): T | null {
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
    override validate(value: any): void | ValidationFailure {
        if(typeof value !== 'object' || !value || Array.isArray(value))
            return new ValidationFailure(value, "Must be an object.");

        for(const key of this.#schemaKeys) {
            const field = this.schema[key];     
            const validationFailure = field.validate(value[key]);
            if(validationFailure) return new ValidationFailure(value, `'${String(key)}': ${validationFailure.reason}`);
        }

        for(const key in value) {
            if(!this.#schemaKeys.has(key)) return new ValidationFailure(value, "Extra properties are not permitted.");
        }
    }
}