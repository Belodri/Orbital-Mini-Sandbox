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