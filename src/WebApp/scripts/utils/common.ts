/**
 * Creates a RegExp that matches or captures any of the strings in the given array.
 * @param strings   Strings to include in the regex. Longer strings will be matched before shorter ones. Regex from char-only arrays will be much more efficient. 
 * @param mode      Whether the resulting regex should capture or match. Defaults to "match".
 * @param flags     Flags for the RegExp object. Defaults to "g".
 * @returns         The RegExp object that will match/capture any of the provided strings, or `null` of the array is empty or contains only empty strings.
 */
export function createRegexFromStrings(strings: string[], mode: "capture" | "match" = "match", flags: string = "g"): RegExp | null {
    const uniqueStrings = new Set(strings.filter(s => s.length));
    if(!uniqueStrings.size) return null;

    const words: string[] = [];
    const chars: string[] = [];

    for(const string of uniqueStrings) {
        if(string.length === 1) chars.push(string);
        else words.push(string);
    }

    const escapeGeneral = (str: string) => str.replace(/[.*+?^${}()|[\]\\]/g, `\\$&`);
    const escapeForCharSet = (char: string) => char.replace(/[\]\\^-]/g, `\\$&`);

    const patterns: string[] = words.sort((a, b) => b.length - a.length).map(escapeGeneral);    // Allow longer words to match first

    if(chars.length) patterns.push(`[${chars.map(escapeForCharSet).join("")}]`);
    
    const pattern = patterns.length === 1
        ? mode === "capture" 
            ? `(${patterns[0]})` 
            : patterns[0]
        : mode === "capture"
            ? `(${patterns.join("|")})` 
            : `(?:${patterns.join("|")})`;
    
    return new RegExp(pattern, flags);
}

/**
 * Retrieve a potentially nested value from a object by a string key.  
 * If an exact match of the flat key `{ "a.b": 1 }` is present at the root, it is prioritized over nested keys `{ a: { b: 2} }`.
 * @param record    The object to search.
 * @param key       A property with the notation a.b.c
 * @returns         The value of the property found. `undefined` if the property is not found.
 */
export function getProperty(record: any, key: string): unknown {
    if(!key || record === null || record === undefined) return undefined;
    if( key in record ) return record[key];
    let current = record;
    for(const segment of key.split(".")) {
        if(current === null || current === undefined) return undefined;
        const type = typeof current;
        if( type !== "object" && type !== "function" ) return undefined;
        if(segment in current) current = current[segment];
        else return undefined;
    }
    return current;
}
