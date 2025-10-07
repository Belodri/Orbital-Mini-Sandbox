interface Command<T = any> {
    value: T;
    resolve: (value: T) => void;
    reject: (reason?: any) => void;
    isSettled: boolean;
}

export class DeferredResolverResetError extends Error {
    constructor(msg: string = "DeferredResolver was reset.") {
        super(msg);
        this.name = "DeferredResolverResetError";
    }
}

/**
 * Manages a queue of synchronous commands, decoupling their immediate execution
 * from the asynchronous resolution of their resulting Promises.
 */
export default class DeferredResolver {
    static readonly QUEUE_CLEARED_REJECTION_ERROR = new DeferredResolverResetError();

    constructor(logError: (...data: any[]) => void = console.error) {
        this.#logError = logError;
    }

    #logError: (...data: any[]) => void;
    #commandPool: Command<any>[] = [];
    #nextResolveQueue: Command<any>[] = [];
    #processingQueue: Command<any>[] = [];
    #isResolving: boolean = false;

    /**
     * Executes a synchronous function immediately.
     * The returned Promise resolves to the return value of the executed function during the next `resolve()` call.
     * 
     * Calling code is responsible for handling errors thrown during the immediate execution!
     * @param fn    Function to execute immediately.
     * @returns     A Promise that resolves to the return value of the executed function after `resolve()` is called.
     */
    execute<T>(fn: [T] extends [PromiseLike<any>] 
        ? (ERROR: "DeferredResolver.execute() cannot be called with an async function or a function that returns a Promise.") => any 
        : () => T
    ): Promise<T>;
    execute<T>(fn: () => T): Promise<T> {
        // Execute directly, any error thrown must be caught by calling code.
        const value = fn();

        // If no error, return a promise that'll later resolve to the returned value.
        return new Promise<T>((resolve, reject) => {
            this.#addToResolveQueue(value, resolve, reject);
        });
    }

    /** 
     * Resolves all promises issued since the last `resolve()` or `reset()` call.
     * Any commands issued during the `resolve()` execution are deferred until the next call.
     */
    resolve(): void {
        if(this.#isResolving) throw new Error("DeferredResolver: Cannot call resolve() while another resolve() is already in progress.");
        this.#isResolving = true;

        [this.#nextResolveQueue, this.#processingQueue] = [this.#processingQueue, this.#nextResolveQueue];

        for(let i = 0; i < this.#processingQueue.length; i++) {
            const cmd = this.#processingQueue[i];

            try {
                cmd.resolve(cmd.value);
            } catch(err) {
                this.#logError("DeferredResolver: Error thrown from promise resolution:", err);
            } finally {
                cmd.isSettled = true;
            }
        }

        if(this.#processingQueue.length) this.#releaseQueue(this.#processingQueue);
        this.#isResolving = false;
    }

    /** Rejects all pending promise resolutions and fully resets the handler. */
    reset(): void {
        this.#rejectQueue(this.#nextResolveQueue);
        this.#rejectQueue(this.#processingQueue);

        this.#releaseQueue(this.#nextResolveQueue);
        this.#releaseQueue(this.#processingQueue);
        this.#isResolving = false;
    }

    /** Adds a command to the resolve queue. */
    #addToResolveQueue<T>(value: T, resolve: (v: T) => void, reject: (r?: any) => void): void {
        const cmd: Command<T> = this.#commandPool.pop() 
            ?? { value: undefined, resolve: undefined!, reject: undefined!, isSettled: false };

        cmd.value = value;
        cmd.resolve = resolve;
        cmd.reject = reject;
        cmd.isSettled = false;

        this.#nextResolveQueue.push(cmd);
    }

    /** Reject all unhandled commands in the queue. */
    #rejectQueue(queue: Command[]): void {
        for(let i = 0; i < queue.length; i++) {
            const cmd = queue[i];
            if(cmd.isSettled) continue;

            try {
                cmd.reject(DeferredResolver.QUEUE_CLEARED_REJECTION_ERROR);
            } catch(err) {
                this.#logError("DeferredResolver: Error thrown while rejecting promise during clear():", err);
            } finally {
                cmd.isSettled = true;
            }
        }
    }

    /** Returns command objects to the pool and clears the passed queue. */
    #releaseQueue(queue: Command[]): void {
        for(let i = 0; i < queue.length; i++) {
            const cmd = queue[i];
            // Clear potential closures
            cmd.resolve = undefined!;
            cmd.reject = undefined!;

            this.#commandPool.push(cmd);
        }
        queue.length = 0;
    }
}
