export const SUPPORTED_BUBBLING_EVENTS = [
    "keydown", "keyup", "keypress",
    "submit", "input", "change", "reset",
    "focusin", "focusout",
    "dragstart", "drag", "dragend", "dragenter", "dragleave", "dragover", "drop",
    "pointerdown", "pointerup", "pointermove", "pointerover", "pointerout",
] as const;

export const SUPPORTED_NON_BUBBLING_EVENTS = [
    "blur", "focus", "scroll", "pointerenter", "pointerleave"
] as const;

export type BubblingType = typeof SUPPORTED_BUBBLING_EVENTS[number];
export type NonBubblingType = typeof SUPPORTED_NON_BUBBLING_EVENTS[number];
export type EventType = BubblingType | NonBubblingType;
export type ElementHandlerFunction = (e: ManagedEvent<Event>) => void;
export type GetElementHandlerFunc = (context: EventContext) => ElementHandlerFunction | null;

export interface IEventTypeHandler {
    readonly type: EventType;
    readonly isBubbling: boolean;
    add(target: HTMLElement): void;
    remove(target: HTMLElement): void;
}

export class BubblingEventTypeHandler implements IEventTypeHandler {
    readonly #getElementHandler: GetElementHandlerFunc;
    readonly #root: HTMLElement;
    readonly #cssSelector: string;
    #count: number = 0;

    readonly type: BubblingType;
    readonly isBubbling = true;

    constructor(type: BubblingType, root: HTMLElement, getElementHandler: GetElementHandlerFunc) {
        this.type = type;
        this.#root = root;
        this.#getElementHandler = getElementHandler;
        this.#cssSelector = `[data-on:${type}]`;
    }

    add(): void {
        if(!this.#count) this.#root.addEventListener(this.type, this.#listener);
        this.#count++
    }

    remove(): void {
        if(this.#count) {
            this.#count--;
            if(!this.#count) this.#root.removeEventListener(this.type, this.#listener);
        }
    }

    #listener = (event: Event): void => {
        if( !(event.target instanceof HTMLElement) ) return;

        let currentElement = event.target.closest(this.#cssSelector);
        if(!currentElement) return;

        const managedEvent = new ManagedEvent(event);

        while(currentElement && !managedEvent.propagationStopped) {
            const ctx = EventContext.fromElement(currentElement as HTMLElement, this.type);

            if(ctx) {
                const handler = this.#getElementHandler(ctx);
                if(handler) handler(managedEvent);
            }

            currentElement = currentElement.parentElement?.closest(this.#cssSelector) ?? null;
        }
    }
}

export class NonBubblingEventTypeHandler implements IEventTypeHandler {
    readonly #getElementHandler: GetElementHandlerFunc;
    readonly #attached: WeakMap<HTMLElement, (e: Event) => void> = new WeakMap();

    readonly type: NonBubblingType;
    readonly isBubbling = false;

    constructor(type: NonBubblingType, getElementHandler: GetElementHandlerFunc) {
        this.type = type;
        this.#getElementHandler = getElementHandler;
    }

    add(target: HTMLElement): void {
        const ctx = EventContext.fromElement(target as HTMLElement, this.type);
        if(!ctx) return;

        const handler = this.#getElementHandler(ctx);
        if(!handler) return;
        
        const listenerFunc = (event: Event): void => handler(new ManagedEvent(event));

        this.#attached.set(target, listenerFunc);
        target.addEventListener(this.type, listenerFunc);
    }

    remove(target: HTMLElement): void {
        const listenerFunc = this.#attached.get(target);
        if(listenerFunc) target.removeEventListener(this.type, listenerFunc);
    }
}

export class EventContext {
    static readonly SUPPORTED = Object.freeze({
        BUBBLING: <ReadonlySet<BubblingType>> new Set(SUPPORTED_BUBBLING_EVENTS),
        NONBUBBLING: <ReadonlySet<NonBubblingType>> new Set(SUPPORTED_NON_BUBBLING_EVENTS),
    });

    static isBubblingEvent(event: any): event is BubblingType {
        return EventContext.SUPPORTED.BUBBLING.has(event);
    }

    static isNonBubblingEvent(event: any): event is NonBubblingType {
        return EventContext.SUPPORTED.NONBUBBLING.has(event);
    }

    static isEventType(event: any): event is EventType {
        return EventContext.isBubblingEvent(event) 
            || EventContext.isNonBubblingEvent(event);
    }

    static fromElementAll(element: HTMLElement): EventContext[] {
        const arr: EventContext[] = [];

        for(const key in element.dataset) {
            const event = EventContext.eventFromDatasetKey(key);
            if(!event) continue;

            const value = element.dataset[key];
            if(!value) continue;

            const data = EventContext.#fromString(event, value);
            if(data) arr.push(data)
        }

        return arr;
    }

    static fromElement(element: HTMLElement, event: EventType): EventContext | undefined {
        const dataStr = element.dataset[`on:${event}`];
        if(dataStr) return EventContext.#fromString(event, dataStr);
    }

    /** @throws If the SourceString is malformed. */
    static #fromString(event: EventType, sourceString: string): EventContext {
        const parts = sourceString.split(":");
        if(parts.length === 2 && parts.every(Boolean)) return new EventContext(event, parts[0], parts[1]);
        throw new Error(`Malformed dataset string: "${sourceString}" for event "${event}".`);
    }

    static eventFromDatasetKey(key: string): EventType | undefined {
        if(key.length > 3 && key.startsWith("on:")) {
            const event = key.substring(3);
            if(EventContext.isEventType(event)) return event;
        }
    }

    static createDatasetKey(event: EventType): string { return `on:${event}`; }
    static createDatasetValue(cid: string, eventHandlerName: string): string { return `${cid}:${eventHandlerName}`; }

    static writeToDataset(element: HTMLElement, event: EventType, cid: string, eventHandlerName: string): void {
        const key = EventContext.createDatasetKey(event);
        const value = EventContext.createDatasetValue(cid, eventHandlerName);
        element.dataset[key] = value;
    }

    readonly cid: string;
    readonly event: EventType;
    readonly eventHandlerName: string;
    readonly isBubbling: boolean;

    constructor(event: EventType, cid: string, eventHandlerName: string) {
        this.cid = cid;
        this.event = event;
        this.eventHandlerName = eventHandlerName;
        this.isBubbling = !EventContext.isNonBubblingEvent(event);
    }
}

/**
 * Handles event listeners dynamically for elements with `data-on:<EventType>="<ComponentId>:<HandlerFunction>"`.
 * 
 * If nested elements have the same EventType, ComponentId, and HandlerFunction, the event bubbles up if possible.
 * Handlers can use the passed {@link ManagedEvent} to stop propagation, prevent default behaviour, and to access the browser's Event object. 
 * 
 * 
 * @example
 * ```html
 * <button type="submit" data-on:submit="41:handleSubmit">Save</button>
 * ```
 */
export default class DOMEventManager {
    static readonly #ATTRIBUTES_TO_TYPES: ReadonlyMap<string, EventType> = new Map([
        ...SUPPORTED_BUBBLING_EVENTS,
        ...SUPPORTED_NON_BUBBLING_EVENTS
    ].map(type => [`data-${EventContext.createDatasetKey(type)}`, type]));

    static readonly #ALL_ATTRIBUTES_CSS_SELECTOR: string = [...DOMEventManager.#ATTRIBUTES_TO_TYPES.keys()]
        .map(attr => `[${attr.replace(":", "\\:")}]`)
        .join(",");
    
    #root: HTMLElement;
    #getElementHandler: GetElementHandlerFunc;
    #observer: MutationObserver;
    #eventTypeHandlers: Map<EventType, IEventTypeHandler> = new Map();
    
    /**
     * @param root The root element the manager should attach bubbling listeners to. 
     * @param getElementHandler Function to get handler function for a given event. 
     */
    constructor(root: HTMLElement, getElementHandler: GetElementHandlerFunc) {
        this.#root = root;
        this.#getElementHandler = getElementHandler;

        this.#observer = new MutationObserver(this.#observerCallback);
        this.#observer.observe(root, {
            subtree: true,
            childList: true,
            attributes: true,
            attributeFilter: [...DOMEventManager.#ATTRIBUTES_TO_TYPES.keys()],
            attributeOldValue: true,
        });

        this.#applyToTree(this.#root, this.#handleAddedNode);   // Initial parse
    }

    _destroy(): void {
        this.#observer.disconnect();
        this.#eventTypeHandlers.clear();
    }

    #observerCallback = (records: MutationRecord[]): void => {
        for(const mutation of records) {    
            if(mutation.type === "attributes") this.#handleAttributeMutation(mutation);
            else if(mutation.type === "childList") {
                for(const node of mutation.addedNodes) {
                    if(node instanceof HTMLElement) this.#applyToTree(node, this.#handleAddedNode);
                }

                for(const node of mutation.removedNodes) {
                    if(node instanceof HTMLElement) this.#applyToTree(node, this.#handleRemovedNode);
                }
            }
        }
    }

    #handleAttributeMutation(mutation: MutationRecord): void {
        const target = mutation.target as HTMLElement;  // Safe because the observer filters by data-* attributes, which are a property of the HTMLElement interface.
        const name = mutation.attributeName!;   // Safe because the mutation observer filters by known attribute types.
        const event = DOMEventManager.#ATTRIBUTES_TO_TYPES.get(name)!; // Built from the same source as the mutation observer's filter.

        const newValue = target.getAttribute(name);
        let typeHandler = this.#eventTypeHandlers.get(event);

        if(newValue === null && !typeHandler) return;

        if(!typeHandler) typeHandler = this.#createTypeHandler(event);

        const oldValue = mutation.oldValue;

        const changed = oldValue && newValue && oldValue !== newValue;
        const remove = changed || oldValue && !newValue;
        const add = changed || !oldValue && newValue;

        if(remove) typeHandler.remove(target);
        if(add) typeHandler.add(target);
    }

    #applyToTree(element: HTMLElement, action: (element: HTMLElement) => void): void {
        action(element);
        if(element.hasChildNodes()) {
            element.querySelectorAll<HTMLElement>(DOMEventManager.#ALL_ATTRIBUTES_CSS_SELECTOR)
                .forEach(action);
        }
    }

    #handleAddedNode = (element: HTMLElement): void => {
        const contexts = EventContext.fromElementAll(element);
        for(const ctx of contexts) {
            const typeHandler = this.#eventTypeHandlers.get(ctx.event)
                ?? this.#createTypeHandler(ctx.event);
            typeHandler.add(element);
        }
    }

    #handleRemovedNode = (element: HTMLElement): void => {
        const contexts = EventContext.fromElementAll(element);
        for(const ctx of contexts) {
            const typeHandler = this.#eventTypeHandlers.get(ctx.event);
            if(typeHandler) typeHandler.remove(element);
        }
    }

    #createTypeHandler(event: EventType): IEventTypeHandler {
        const typeHandler = EventContext.isBubblingEvent(event)
            ? new BubblingEventTypeHandler(event, this.#root, this.#getElementHandler)
            : new NonBubblingEventTypeHandler(event, this.#getElementHandler);

        this.#eventTypeHandlers.set(event, typeHandler);
        return typeHandler;
    }
}

export class ManagedEvent<T extends Event> {
    #event: T;
    #propagationStopped: boolean = false;

    constructor(event: T) {
        this.#event = event;
    }

    get event(): T { return this.#event; }
    get propagationStopped(): boolean { return this.#propagationStopped; }

    preventDefault(): void { this.#event.preventDefault(); }
    stopPropagation(): void { this.#propagationStopped = true; }
}