import { createRegexFromStrings } from "./common";

/**
 * Map of characters to their respective respective HTML entity equivalent.  
 */
export const HTML_CHARACTER_MAP = {
    "&": "&amp;",
    "<": "&lt;",
    ">": "&gt;",
    '"': "&quot;",
    "'": "&apos;", 
    "`": "&#96;",
    "=": "&#61;",
    "(": "&lpar;",
    ")": "&rpar;",
    "|": "&shortmid;"
} as const;

/**
 * Finds and captures characters that should be replaced with their respective HTML entity equivalent.  
 */
export const ESCAPE_CHAR_REGEX = (() => {
    const regex = createRegexFromStrings(Object.keys(HTML_CHARACTER_MAP));
    if(!regex) throw new Error("Unable to create regex for escape characters.");
    return regex;
})();

/**
 * Finds and captures content that meets the following criteria:
 * 1. Content must be enclosed by exactly two opening and two closing curly braces.
 * 2. Content cannot contain any curly braces itself.
 * 3. Content cannot be empty or contain only whitespace.
 */
export const MUSTACHE_REGEX = /(?<!{){{([^{}]*\S[^{}]*)}}(?!})/g;

/**
 * Utility to enable the creation of DocumentFragments from a given HTML string template.
 * Allows moustache placeholders (`{{...}}`) in attribute values and textNodes values the HTML, 
 * which are replaced with provided, potentially untrusted data upon creation of the DocumentFragment. Nested moustaches are not resolved.
 * 
 * @example
 * ```html
 * <label for="{{id}}">Mass</label>
 * <input type="number" id="{{id}}">
 * ```
 */
export default class HTMLStringTemplate {
    readonly #baseFragment: DocumentFragment;

    /**
     * Creates a new instance that can be used to create multiple fragments of the same HTML string.
     * @param htmlString    The HTML string to serve as the template. Is inherently trusted and not validated or sanitized.
     */
    constructor(htmlString: string) {
        this.#baseFragment = document.createRange().createContextualFragment(htmlString);
    }

    /**
     * Creates a DocumentFragment from this template.
     * @param data  Data to populate any moustache (`{{...}}`) placeholders in the HTML with.  
     *              Values containing sensitive characters have these characters replaced with their respective HTML entity equivalent (see {@link HTML_CHARACTER_MAP}).
     * @returns     The populated DocumentFragment.
     */
    toFragment(data: Record<string, string> = {}): DocumentFragment {
        const clone = this.#baseFragment.cloneNode(true) as DocumentFragment;

        const cleanData = HTMLStringTemplate.#cleanData(data);
        HTMLStringTemplate.#injectData(clone, cleanData);

        return clone;
    }

    /**
     * While the site only runs locally, presets are sharable with others and are thus a potential attack vector for XSS injection.
     */
    static #cleanData(unsafeData: Record<string, string>): Record<string, string> {
        const replacer = (char: string): string => HTML_CHARACTER_MAP[char as keyof typeof HTML_CHARACTER_MAP];

        const cleanEntries = Object.entries(unsafeData)
            .map(([k, v]) => [
                k,
                v?.replaceAll(ESCAPE_CHAR_REGEX, replacer) ?? ""
            ]);
        
        return Object.fromEntries(cleanEntries);
    }

    static #injectData(clone: DocumentFragment, data: Record<string, string>): void {
        const replacer = (match: string, matchedKey: string): string => data[matchedKey] ?? match;

        const walker = document.createTreeWalker(
            clone, 
            NodeFilter.SHOW_ELEMENT | NodeFilter.SHOW_TEXT,
            (node) => {
                const name = node.nodeName.toLowerCase();
                return name === "script" || name === "style"
                    ? NodeFilter.FILTER_REJECT
                    : NodeFilter.FILTER_ACCEPT;
            }
        );

        while(walker.nextNode()) {
            const node = walker.currentNode;

            if(node instanceof Element) {
                for(const attr of node.attributes) {
                    if(attr.value.includes("{{")) attr.value = attr.value.replaceAll(MUSTACHE_REGEX, replacer);
                }
            } else if(node.nodeValue && node.nodeValue.includes("{{")) {
                node.nodeValue = node.nodeValue.replaceAll(MUSTACHE_REGEX, replacer);
            }
        }
    }
}
