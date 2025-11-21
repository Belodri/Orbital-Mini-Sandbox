
export default class WeakValueMap<K, V extends object> implements Map<K, V> {
    #map: Map<K, WeakRef<V>> = new Map();

    #registry = new FinalizationRegistry<readonly [K, WeakRef<V>]>(([key, ref]) => {
        if(this.#map.get(key) === ref) this.#map.delete(key);
    });

    constructor();
    constructor(iterable?: Iterable<readonly [K, V]> | null);
    constructor(entries?: readonly(readonly[K, V])[] | null);
    constructor(entr?: Iterable<readonly [K, V]> | readonly(readonly[K, V])[] | null) {
        if(!entr) return;
        for(const [key, value] of entr) this.set(key, value);
    }

    /** 
     * Note: This might be inaccurate if items have been GC'd but not yet finalized.
     * @inheritdoc
     */
    get size(): number { return this.#map.size; }

    /**
     * Gets a much more accurate size of the map by iterating over all items that haven't been garbage collected.
     * This size is valid at the very least until the current callstack + microtasks finish (unless the browser is running out of memory).
     */
    get sizeExact(): number { 
        let count = 0;
        for(const _ of this.entries()) count++;
        return count;
    }

    clear(): void {
        for(const ref of this.#map.values()) this.#registry.unregister(ref);
        this.#map.clear();
    }

    delete(key: K): boolean {
        const ref = this.#map.get(key);
        if(!ref) return false;

        this.#registry.unregister(ref);
        return this.#map.delete(key);
    }

    forEach(callbackfn: (value: V, key: K, map: Map<K, V>) => void, thisArg?: unknown): void {
        for(const [key, value] of this.entries()) {
            callbackfn.call(thisArg, value, key, this);
        }
    }

    get(key: K): V | undefined {
        const ref = this.#map.get(key);
        if(!ref) return undefined;

        const val = ref.deref();
        if(val === undefined) {
            this.#map.delete(key);
            this.#registry.unregister(ref);
            return undefined;
        }
        return val;
    }

    has(key: K): boolean {
        return this.get(key) !== undefined;
    }

    set(key: K, value: V): this {
        const oldRef = this.#map.get(key);
        if(oldRef) this.#registry.unregister(oldRef);

        const newRef = new WeakRef(value);
        this.#map.set(key, newRef);
        this.#registry.register(value, [key, newRef], newRef);
        return this;
    }

    *entries(): MapIterator<[K, V]> {
        for(const [key, ref] of this.#map.entries()) {
            const val = ref.deref();
            if(val === undefined) {
                this.#map.delete(key);
                this.#registry.unregister(ref);
            } else yield [key, val];
        }
    }

    *keys(): MapIterator<K> {
        for(const [key, _] of this.entries()) yield key;
    }

    *values(): MapIterator<V> {
        for(const [_, val] of this.entries()) yield val;
    }

    [Symbol.iterator](): MapIterator<[K, V]> {
        return this.entries();
    }

    readonly [Symbol.toStringTag]: string = "WeakValueMap"
}
