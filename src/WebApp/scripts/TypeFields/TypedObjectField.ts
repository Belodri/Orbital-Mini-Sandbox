import BaseTypeField, { ITypeField } from "./BaseTypeField";
import ValidationFailure from "./ValidationFailure";

type SchemaDefinition = Record<string, ITypeField<any>>;

type InferSchemaType<T extends SchemaDefinition> = {
    [K in keyof T]: T[K]['value'];
};

export type TypedObjectFieldOptions<T extends SchemaDefinition> = {
    /** The schema definition used to validate the object's properties. */
    schema: T;
}

export default class TypedObjectField<
    TSchema extends SchemaDefinition,
    TValue extends InferSchemaType<TSchema> = InferSchemaType<TSchema>
> extends BaseTypeField<TValue, TypedObjectFieldOptions<TSchema>> {

    readonly #schemaKeys: ReadonlySet<keyof TSchema>;

    constructor(initialValue: TValue, options: TypedObjectFieldOptions<TSchema>) {
        if(!options.schema || !Object.keys(options.schema).length) throw new Error("Must provide a valid, non-empty schema.");
        super(initialValue, options);

        this.#schemaKeys = new Set(Object.keys(options.schema));
    }

    override getDefaultOptions(): Readonly<TypedObjectFieldOptions<TSchema>> {
        return { schema: {} as TSchema };
    }

    /**
     * Property mismatches between data and schema will cause a ValidationFailure.
     * @inheritdoc
     */
    override validate(data: any): void | ValidationFailure {
        const Fail = ValidationFailure;

        if(typeof data !== 'object' || !data || Array.isArray(data)) return new Fail(data, "Must be an object.");

        const dataKeys = new Set(Object.keys(data));
        const anyKeysUnknown = !dataKeys.isSubsetOf(this.#schemaKeys);
        const anyKeysMissing = !this.#schemaKeys.isSubsetOf(dataKeys);

        if(anyKeysUnknown || anyKeysMissing) {
            const keyFails = [
                anyKeysUnknown ? `Unknown properties: ${[...dataKeys.difference(this.#schemaKeys)].join(", ")}.` : "",
                anyKeysMissing ? `Missing required properties: '${[...this.#schemaKeys.difference(dataKeys)].join(", ")}'.` : ""
            ].filter(Boolean);
            return new Fail(data, keyFails.join(" "));
        }

        for(const key of this.#schemaKeys) {
            const field = this.options.schema[key];
            const validationFailure = field.validate(data[key]);
            if(validationFailure) return new Fail(data, `'${String(key)}': ${validationFailure.reason}`);
        }
    }

    /**
     * Any properties not in the schema are stripped.
     * @inheritdoc
     */
    override cast(value: any): TValue | null {
        if(typeof value !== 'object' || !value || Array.isArray(value)) return null;
        
        const newObj = {} as TValue;

        for(const key of this.#schemaKeys) {
            const field = this.options.schema[key];
            const castValue = field.cast(value[key]);   // Let the field decide how to cast missing properties.

            if(castValue === null) return null;
            else newObj[key] = castValue;
        }

        return newObj;
    }
}