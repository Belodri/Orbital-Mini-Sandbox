import ValidationField from "../ValidationFields/ValidationField";
import BooleanField from "../ValidationFields/BooleanField";
import NumberField from "../ValidationFields/NumberField";
import StringField from "../ValidationFields/StringField";

type UnionToIntersection<U> = (U extends any ? (k: U) => void : never) extends ((k: infer I) => void) ? I : never;

type FlattenFields<T, Prefix extends string = ""> = {
    [K in keyof T]: K extends string
        ? T[K] extends ValidationField<any>
            ? { [Path in `${Prefix}${K}`]: T[K] }
            : T[K] extends Record<string, any>
                ? FlattenFields<T[K], `${Prefix}${K}.`>
                : never
        : never
}[keyof T];

type FlatFieldsIntersection = UnionToIntersection<FlattenFields<typeof InputValidationFieldSchema>>;

/**
 * A flattened map of all {@link ValidationField} instances defined in the schema.  
 * Keys are dot-notation strings (e.g., `"physics.sim.timeStep"`) representing the path 
 * to the field within the source schema.
 */
export type InputValidationFieldRecord = {
    [K in keyof FlatFieldsIntersection]: FlatFieldsIntersection[K];
}

/**
 * All valid dot-notation paths within the {@link InputValidationFieldSchema}.
 */
export type InputValidationFieldKey = keyof InputValidationFieldRecord;

/** 
 * Global validation fields for user input fields. Acts as the SSOT for input validator definitions.  
 * All {@link ValidationField} instances must be unique within the schema to avoid configuration errors.
 */
const InputValidationFieldSchema = {
    physics: {
        sim: {
            timeStep: new NumberField(),
            theta: new NumberField({ min: 0, max: 1, step: 0.001 }),
            G: new NumberField(),
            epsilon: new NumberField({ min: 0.001, step: 0.001 }),
        },
        body: {
            enabled: new BooleanField(),
            mass: new NumberField(),
            posX: new NumberField(),
            posY: new NumberField(),
            velX: new NumberField(),
            velY: new NumberField(),
        }
    },
    app: {
        sim: {
            bgColor: new StringField(),  // TODO: Change to ColorField once that is implemented
            enableOrbitPaths: new BooleanField(),
            enableBodyLabels: new BooleanField(),
            enableVelocityTrails: new BooleanField(),
        },
        body: {
            name: new StringField({ maxLength: 30, wellFormed: true }),
            tint: new StringField(),    // TODO: Change to ColorField once that is implemented
        }
    }
} as const;

/**
 * Static registry and manager for application-wide input validation fields.
 */
export default class InputValidators {
    private constructor() {};

    static #fields: Readonly<InputValidationFieldRecord>;
    static #keys: ReadonlySet<InputValidationFieldKey>;

    static {
        const fields = this.#flattenSchema(InputValidationFieldSchema) as InputValidationFieldRecord;
        this.#fields = Object.freeze(fields);
        this.#keys = new Set(Object.keys(this.#fields) as InputValidationFieldKey[]);
    }

    /** Flattened, immutable record of all validation fields. */
    static get fields() { return this.#fields; }
    /** Set containing all valid input validation keys. */
    static get keys() { return this.#keys; }
    /** Original, hierarchical source of truth schema object. */
    static get baseFields() { return InputValidationFieldSchema; }

    /**
     * Type guard to check if a given string is a valid key for an input validation field.
     */
    static isFieldKey(str: string): str is InputValidationFieldKey {
        return this.#keys.has(str as InputValidationFieldKey);
    }

    /**
     * Gets the specific {@link ValidationField} instance associated with the provided key.
     */
    static getField<K extends InputValidationFieldKey>(key: K): InputValidationFieldRecord[K] {
        const field = this.#fields[key];
        if(__DEBUG__ && !field) throw new Error(`Field key "${key}" exists in types but not in runtime record.`); 
        return field;
    }

    static #flattenSchema(
        obj: Record<string, any>, 
        prefix: string = "", 
        result: Record<string, ValidationField<any>> = {},
        seen: WeakSet<ValidationField<any>> = new WeakSet()
    ): Record<string, ValidationField<any>> {
        for(const [k, v] of Object.entries(obj)) {
            if (k.includes(".")) throw new Error(`Schema keys cannot contain dots: "${k}"`);
        
            const path = prefix ? `${prefix}.${k}` : k;

            if (v instanceof ValidationField) {
                if(seen.has(v)) throw new Error(`Duplicate field instance for key "${k}".`);
                seen.add(v);
                result[path] = v;
            }
            // Ensure POJO
            else if (typeof v === "object" && v !== null && Object.getPrototypeOf(v) === Object.prototype) {
                this.#flattenSchema(v, path, result, seen);
            } 
            else throw new Error(`Invalid value at "${path}": Expected ValidationField or Plain Object, got ${v?.constructor?.name || typeof v}`);
        }

        return result
    }
}
