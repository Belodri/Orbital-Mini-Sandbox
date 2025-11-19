export {}

declare global {
    interface Math {
        clamp: typeof clamp;
    }

    interface Number {
        approxEquals: typeof approxEquals;
        isBetween: typeof isBetween;
    }
}

/**
 * Clamp a number between a minimum and a maximum value, inclusively.
 * @param num   The number to clamp. 
 * @param min   The minimum allowed value.
 * @param max   The maximum allowed value.
 */
function clamp(num: number, min: number, max: number): number {
    return Math.min(max, Math.max(min, num));
}

/**
 * Test for approximate equality using a tolerance.
 * @param other     The other number.
 * @param epsilon   The tolerance. Defaults to 1e-12
 */
function approxEquals(this: number, other: number, epsilon: number = 1e-12): boolean {
    return Math.abs(this - other) < epsilon;
}

/**
 * Checks if the number is between two bounding numbers.
 * @param boundA        Other number to compare.
 * @param boundB        Other number to compare.
 * @param exclusive     Exclude the bounding values?
 */
function isBetween(this: number, boundA: number, boundB: number, exclusive?: boolean): boolean {
    const min = Math.min(boundA, boundB);
    const max = Math.max(boundA, boundB);
    return exclusive ? (this > min) && (this < max) : (this >= min) && (this <= max);
}

Object.defineProperties(Math, {
    clamp: { value: clamp }
});

Object.defineProperties(Number.prototype, {
    approxEquals: { value: approxEquals },
    isBetween: { value: isBetween },
});
