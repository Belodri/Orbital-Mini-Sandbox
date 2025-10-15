/** Event manager for events with 0 argument callbacks. */
export interface IEventHandler<TEvent extends string> {
    /** Call all registered listeners for a given event. */
    callEventListeners(event: TEvent): void;
    /**
     * Registers a listener for a given event.
     * @param event     The event for which to register the listener.
     * @param fn        The listener callback function.
     * @returns         The id of the registered listener.
     */
    registerEventListener(event: TEvent, fn: () => void): number
    /**
     * Remove the listener with the given id.
     * @param event     The event from which to remove the listener.
     * @param id        The id of the listener to remove.
     */
    removeEventListener(event: TEvent, id: number): void;
    /**
     * Clear all listeners.
     * @param events    If given, only clears listeners for contained events.
     */
    clear(events?: TEvent[]): void;
}

export default class EventHandler<TEvent extends string> implements IEventHandler<TEvent> {
    #nextEventId = 0;
    #events: Map<TEvent, Map<number, () => void>> = new Map();

    callEventListeners(event: TEvent): void {
        for(const fn of this.#events.get(event)?.values() ?? []) fn();
    }

    registerEventListener(event: TEvent, fn: () => void): number {
        let typeMap = this.#events.get(event);
        if(!typeMap) {
            typeMap = new Map() as Map<number, () => void>;
            this.#events.set(event, typeMap);
        }

        const id = ++this.#nextEventId;
        typeMap.set(id, fn);
        return id;
    }

    removeEventListener(event: TEvent, id: number): void {
        this.#events.get(event)?.delete(id);
    }

    clear(events?: TEvent[]): void {
        if(events) for(const event of events) this.#events.delete(event);
        else this.#events.clear();
    }
}
