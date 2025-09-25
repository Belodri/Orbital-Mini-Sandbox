interface Command<T = any> {
    fn: () => T;
    resolve: (value: T) => void;
    reject: (reason?: any) => void;
    result: { success: true; value: T} | { success: false; value: Error };
}

/**
 * Asynchronous command queue.
 * 
 * The lifecycle per frame is:
 * - `enqueue()`: Queues work for the next render frame.
 * 
 * 1. `process()`: Called at the start of each render frame. Executes all queued work.
 * 2. `resolveProcessed()`: Called once the rendering of the frame has finished. Resolves all Promises.
 */
export default class CommandQueue {
    static readonly #ERROR_QUEUE_CLEARED_REJECTION = new Error("CommandQueue was cleared.");
    static readonly #ERROR_COMMAND_RESULT_UNDEFINED = new Error("Command result undefined.");

    #commandPool: Command[] = [];
    #nextFrameQueue: Command[] = [];
    #processingQueue: Command[] = [];
    #isResolved: boolean = true;

    /**
     * Enqueues a function to be executed during the next `process()` call.
     * @param fn    Function to execute.
     * @returns     A Promise that resolves with the function's return value after `resolveProcessed()` is called.
     */
    enqueue<T>(fn: () => T): Promise<T>;
    /**
     * Enqueues a function to be executed during the next `process()` call.
     * @param fn    Function to execute.
     * @returns     A Promise that resolves after `resolveProcessed()` is called.
     */
    enqueue(fn: () => void): Promise<void>;
    enqueue<T>(fn: () => T | void): Promise<T | void> {
        return new Promise<T | void>((resolve, reject) => {
            const cmd = this.#commandPool.pop() ?? {
                fn: undefined,
                resolve: undefined,
                reject: undefined,
                result: {
                    success: false,
                    value: CommandQueue.#ERROR_COMMAND_RESULT_UNDEFINED
                }
            };

            cmd.fn = fn;
            cmd.resolve = resolve;
            cmd.reject = reject;
            this.#nextFrameQueue.push(cmd as Command<T>);
        });
    }

    /**
     * Processes all commands currently in the queue.
     * Any further commands enqueued during this function's execution are queued for the following render frame. 
     */
    process(): void {
        if(!this.#isResolved) throw new Error("CommandQueue: resolveProcess() was not called before process().");
        this.#isResolved = false;

        [this.#nextFrameQueue, this.#processingQueue] = [this.#processingQueue, this.#nextFrameQueue];

        for(let i = 0; i < this.#processingQueue.length; i++) {
            const cmd = this.#processingQueue[i];
            try {
                const value = cmd.fn();
                // Mutating the values of `result` technically breaks the atomicity promised 
                // by the discriminated union but as long as the state of cmd.result is guaranteed 
                // to be valid after this try...catch block, this is just an academic "flaw".
                // The alternative would be to assign a new object, which would only create 
                // additional work for the GC.
                cmd.result.value = value;
                cmd.result.success = true;
            } catch(err) {
                cmd.result.value = err instanceof Error ? err : new Error(String(err));
                cmd.result.success = false;
            }
        }
    }

    /**
     * Resolves the Promises for all commands that were run in the last `process()` call.
     * Must be called at the end of each render frame. 
     */
    resolveProcessed(): void {
        for(let i = 0; i < this.#processingQueue.length; i++) {
            const cmd = this.#processingQueue[i];
            try {
                if(cmd.result.success) cmd.resolve(cmd.result.value);
                else cmd.reject(cmd.result.value);
            } catch(err) {
                console.error("CommandQueue: Error thrown from promise resolution:", err);
            }
        }

        this.#release(this.#processingQueue);
        this.#isResolved = true;
    }

    /**
     * Clears all pending commands and pending promise resolutions.
     */
    clear(): void {
        const rejError = CommandQueue.#ERROR_QUEUE_CLEARED_REJECTION;

        for(let i = 0; i < this.#nextFrameQueue.length; i++) {
            try {
                this.#nextFrameQueue[i].reject(rejError);
            } catch(err) {
                console.error("CommandQueue: Error thrown while rejecting promise during clear():", err);
            }
        }

        for(let i = 0; i < this.#processingQueue.length; i++) {
            try {
                this.#processingQueue[i].reject(rejError);
            } catch(err) {
                console.error("CommandQueue: Error thrown while rejecting promise during clear():", err);
            }
        }

        this.#release(this.#nextFrameQueue);
        this.#release(this.#processingQueue);
        this.#isResolved = true;
    }

    /** Returns command objects to the pool and clears the passed queue. */
    #release(queue: Command[]): void {
        for(let i = 0; i < queue.length; i++) {
            const cmd = queue[i];
            // Clear potential closures
            cmd.fn = undefined!;
            cmd.resolve = undefined!;
            cmd.reject = undefined!;

            this.#commandPool.push(cmd);
        }
        queue.length = 0;
    }
}