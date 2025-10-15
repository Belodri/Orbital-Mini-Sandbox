import { NotificationTypes } from "./consts";
import { NotificationSlot } from "./NotificationSlot";

const DEFAULT_CONFIG: NotificationsConfig = {
    containerElementId: "notifications-container",
    slots: 3,
    minRenderIntervalMs: 1000,
    renderDebounceMs: 100,
    minMessageDurationMs: 10000, 
} as const;

type NotificationType = keyof typeof NotificationTypes;

type NotificationsConfig = {
    containerElementId: string,
    slots: number,
    minRenderIntervalMs: number,
    renderDebounceMs: number,
    minMessageDurationMs: number
}

export type NotificationData = {
    type: NotificationType,
    msg: string,
    durationMs: number | null,
    end: number | null,    
}

/** 
 * Utility for displaying temporary, non-modal notification messages to the user.
 * See Docs/Front_End_Alpha.md for details.
 */
export interface INotifications {
    /** Clears all queued and currently displayed notifications. */
    clearAll(): void;
    /** Clears all queued and currently displayed notifications of the given types. */
    clearTypes(types: NotificationType[]): void;
    /**
     * Renders a red error notification message.
     * @param msg               The message to be displayed.
     * @param durationInSeconds For how many seconds should the message be displayed (min set during initialization)? 
     *                          If omitted, the message persists until closed by the user.
     */ 
    error(msg: string, durationInSeconds?: number): void;
    /**
     * Renders a yellow warning notification message.
     * @param msg               The message to be displayed.
     * @param durationInSeconds For how many seconds should the message be displayed (min set during initialization)? 
     *                          If omitted, the message persists until closed by the user.
     */ 
    warn(msg: string, durationInSeconds?: number): void;
    /**
     * Renders a green success notification message.
     * @param msg               The message to be displayed.
     * @param durationInSeconds For how many seconds should the message be displayed (min set during initialization)? 
     *                          If omitted, the message persists until closed by the user.
     */ 
    success(msg: string, durationInSeconds?: number): void;
    /**
     * Renders a blue info notification message.
     * @param msg               The message to be displayed.
     * @param durationInSeconds For how many seconds should the message be displayed (min set during initialization)? 
     *                          If omitted, the message persists until closed by the user.
     */ 
    info(msg: string, durationInSeconds?: number): void;
    /** Destroys the Notifications instance and cleans up references to it and its child elements. */
    destroy(): void;
}

export default class Notifications implements INotifications {
    static #validateConfig(cfg: NotificationsConfig): void {
        const slotsValid = Number.isSafeInteger(cfg.slots) && cfg.slots > 0;
        if(!slotsValid) throw new Error(`Configuration property "slots" must be a safe integer greater than 0.`);

        const minRenderValid = Number.isSafeInteger(cfg.minRenderIntervalMs) && cfg.minRenderIntervalMs >= 0;
        if(!minRenderValid) throw new Error(`Configuration property "minRenderIntervalMs" must be a safe, positive integer.`);

        const renderDebounceValid = Number.isSafeInteger(cfg.renderDebounceMs) && cfg.renderDebounceMs >= 0;
        if(!renderDebounceValid) throw new Error(`Configuration property "renderDebounceMs" must be a safe, positive integer.`);

        const minMessageDurValid = Number.isSafeInteger(cfg.minMessageDurationMs) && cfg.minMessageDurationMs > 0;
        if(!minMessageDurValid) throw new Error(`Configuration property "minMessageDurationMs" must be a safe integer greater than 0.`);

        // Constructor checks for valid element ID.
    }

    #config: NotificationsConfig;
    #slots: NotificationSlot[] = [];
    #queue: NotificationData[] = [];
    #debounceTimeoutId: number | null = null;
    #renderTimeoutId: number | null = null;
    #container: HTMLElement;

    constructor(cfg: Partial<NotificationsConfig> = {}, ) {
        this.#config = { ...DEFAULT_CONFIG, ...cfg };

        Notifications.#validateConfig(this.#config);

        // Configure container
        const container = document.getElementById(this.#config.containerElementId);
        if(!container) throw new Error(`Invalid container with id '${this.#config.containerElementId}'.`);

        // Create slots
        for(let i = 0; i < this.#config.slots; i++) {
            const slot = new NotificationSlot(i, this.#onSlotUserClose);
            this.#slots[i] = slot;
            container.appendChild(slot.element);
        }

        this.#container = container;
    }

    //#region Public API

    clearAll(): void { this.#clear(); }
    clearTypes(types: NotificationType[]): void { this.#clear(types); }
    error(msg: string, durationInSeconds?: number): void { this.#notify("error", msg, durationInSeconds); }
    warn(msg: string, durationInSeconds?: number): void { this.#notify("warn", msg, durationInSeconds); }
    success(msg: string, durationInSeconds?: number): void { this.#notify("success", msg, durationInSeconds); }
    info(msg: string, durationInSeconds?: number): void { this.#notify("info", msg, durationInSeconds); }
    destroy(): void {
        this.clearAll();
        this.#container = null as any;
    }

    //#endregion


    /**
     * Renders a notification message of a given type.
     * @param type      The type of the notification.
     * @param msg       The message to be displayed.
     * @param durationInSeconds For how many seconds should the message be displayed (min set during initialization)? 
     *                          If omitted, the message persists until closed by the user.
     */ 
    #notify(type: NotificationType, msg: string, durationInSeconds?: number): void {
        const durationMs = Number.isSafeInteger(durationInSeconds) 
            ? Math.max(this.#config.minMessageDurationMs, durationInSeconds! * 1000) 
            : null;

        this.#queue.push({ type, msg: msg.trim(), durationMs, end: null });
        this.#debounceRender();
    }
    
    /**
     * Clears all queued and currently displayed notifications.
     * @param types If given, clears only a notifications of the given types. 
     */
    #clear(types?: NotificationType[]) {
        // Clear queue
        this.#queue = types ? this.#queue.filter(data => !types.includes(data.type)) : [];

        // Clear slots
        let clearCount = 0;
        for(const slot of this.#slots) {
            const clear = slot.data && (!types || types.includes(slot.data.type));
            if(clear) {
                slot.clear();
                clearCount++;
            }
        }

        // Stop render loop if all slots were cleared
        if(clearCount === this.#slots.length) {
            if(this.#debounceTimeoutId) window.clearTimeout(this.#debounceTimeoutId);
            this.#debounceTimeoutId = null;
            if(this.#renderTimeoutId) window.clearTimeout(this.#renderTimeoutId);
            this.#renderTimeoutId = null;
        }
        // Otherwise re-render
        else this.#debounceRender();
    }

    
    /** 
     * Called when a notification is closed by the user. 
     * Starts a debounced render to provide near-immediate visual feedback.
     */
    #onSlotUserClose = () => this.#debounceRender();

    /** Debounces calls to the main render loop. */
    #debounceRender(): void {
        if(this.#debounceTimeoutId) window.clearTimeout(this.#debounceTimeoutId);
        this.#debounceTimeoutId = window.setTimeout(this.#render, this.#config.renderDebounceMs);
    }

    /** Renders slots and schedules the next render if any rendered slots are about to expire.  */
    #render = () => {
        this.#debounceTimeoutId = null;
        if(this.#renderTimeoutId) window.clearTimeout(this.#renderTimeoutId);
        this.#renderTimeoutId = null;

        const now = Date.now();
        this.#renderSlots(now);

        // Schedule next render if necessary
        let nextEnd = Infinity;
        for(const {data} of this.#slots) if(data?.end && data.end < nextEnd) nextEnd = data.end;

        if(nextEnd !== Infinity) {
            const nextRender = Math.max(nextEnd - now, this.#config.minRenderIntervalMs);
            this.#renderTimeoutId = window.setTimeout(this.#render, nextRender);
        }
    }

    /**
     * Renders slots, removing expired notifications and rendering queued ones.
     * Non-expired slots remain in the same relative order.
     * @param now The current timestamp.
     */
    #renderSlots(now: number): void {
        const validData: NotificationData[] = [];

        // Compact non-expired data
        let firstChangedIndex = 0;
        let invalidCount = 0;
        for(let i = 0; i < this.#slots.length; i++) {
            const slot = this.#slots[i];
            const isExpired = slot.data?.end && slot.data.end <= now;

            if(slot.data && !isExpired) validData.push(slot.data);
            else {
                if(!firstChangedIndex) firstChangedIndex = i;
                invalidCount++;
            }
        }

        // Try fill empty from queue
        const unqueue = Math.min(invalidCount, this.#queue.length);
        if(unqueue) {
            for(const data of this.#queue.splice(0, unqueue)) {
                // Set end timestamp if possible
                if(data.durationMs) data.end = now + data.durationMs;
                validData.push(data);
            }
        }

        // Update DOM
        for(let i = firstChangedIndex; i < this.#slots.length; i++) {
            const slot = this.#slots[i];

            if(i < validData.length) slot.set(validData[i]);
            else slot.clear();
        }
    }
}
