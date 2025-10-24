export class ValidationError extends Error {
    constructor(msg: string, options?: ErrorOptions) {
        super(msg, options);
        this.name = "ValidationError";
    }
}

export default class ValidationFailure {
    /** The value that failed validation. */
    readonly invalidValue: unknown;
    /** The reason the validation failed. */
    readonly reason: string;

    constructor(invalidValue: unknown, reason: string) {
        this.invalidValue = invalidValue;
        this.reason = reason;
    }

    /** Creates a {@link ValidationError} with the failure's reason as the message. */
    asError(): ValidationError {
        return new ValidationError(this.reason);
    }
}