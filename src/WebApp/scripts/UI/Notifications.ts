const NotificationTypes = {
    error: { elementClass: "error", iconClass: "fa-solid fa-circle-xmark" },
    warn: { elementClass: "warn", iconClass: "fa-solid fa-triangle-exclamation" },
    success: { elementClass: "success", iconClass: "fa-solid fa-check-double" },
    info: { elementClass: "info", iconClass: "fa-solid fa-info" }
} as const;

type NotificationType = keyof typeof NotificationTypes;

type NotificationsConfig = {
    containerElementId: string,
    slots: number,
    minRenderIntervalMs: number,
    renderDebounceMs: number,
    minMessageDurationMs: number
}

type NotificationData = {
    type: NotificationType,
    msg: string,
    durationMs: number | null,
    end: number | null,    
}

class NotificationSlot {
    static readonly CSS_CLASSES = {
        element: "notification",
        closeIcon: "fa-solid fa-xmark",
        hidden: "hidden"
    }

    static readonly #allTypeElementCssClasses = Object.values(NotificationTypes).map(t => t.elementClass);

    readonly index: number;
    readonly element: HTMLDivElement;

    #data?: NotificationData;
    #icon: HTMLElement;
    #span: HTMLSpanElement;
    #boundCloseListener = this.#closeIconListener.bind(this);
    #onUserCloseCallback: () => void;

    get data() { return this.#data; }
    get isCleared() { return !this.#data; }

    constructor(index: number, onUserCloseCallback: () => void) {
        this.index = index;
        this.#onUserCloseCallback = onUserCloseCallback;

        const { element, hidden, closeIcon } = NotificationSlot.CSS_CLASSES;

        const div = document.createElement("div");
        div.classList.add(element, hidden);

        const icon = document.createElement("i");
        const span = document.createElement("span");
    
        const closeI = document.createElement("i");
        closeI.className = closeIcon;
        closeI.addEventListener("pointerdown", this.#boundCloseListener);

        div.appendChild(icon);
        div.appendChild(span);
        div.appendChild(closeI);

        this.element = div;
        this.#icon = icon;
        this.#span = span;
    }

    set(data: NotificationData) {
        const {elementClass, iconClass} = NotificationTypes[data.type];

        this.element.classList.remove(NotificationSlot.CSS_CLASSES.hidden,
            ...NotificationSlot.#allTypeElementCssClasses);

        this.element.classList.add(elementClass);
        this.#icon.className = iconClass;
        this.#span.textContent = data.msg;

        this.#data = data;
    }

    clear() {
        if(!this.#data) return;
        this.#data = undefined;
        this.element.classList.add(NotificationSlot.CSS_CLASSES.hidden);
    }

    #closeIconListener(event: PointerEvent) {
        event.stopPropagation();
        event.preventDefault();
        this.clear();
        this.#onUserCloseCallback();
    }
}

/** 
 * Static utility for displaying temporary, non-modal notification messages to the user.
 * See Docs/Front_End_Alpha.md for details.
 */
export default class Notifications {
    static #instanceField: Notifications;

    /** Default values for the Notifications config. */
    static readonly DEFAULT_CONFIG: NotificationsConfig = {
        containerElementId: "notifications-container",
        slots: 3,
        minRenderIntervalMs: 1000,
        renderDebounceMs: 100,
        minMessageDurationMs: 10000, 
    }

    static get #instance() {
        if(!Notifications.#instanceField) throw new Error("Notifications have not been initialized.");
        return Notifications.#instanceField;
    }

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

    //#region Public API

    /** 
     * Initializes the class. Calling any other method before this one will throw an Error.
     * Repeated initialization calls are safely ignored.
     */
    static init(cfg: Partial<NotificationsConfig> = {}) {
        Notifications.#instanceField ??= new Notifications(cfg);
    }

    /** Clears all queued and currently displayed notifications. */
    static clearAll(): void {
        Notifications.#instance.clear();
    }

    /** Clears all queued and currently displayed notifications of the given types. */
    static clearTypes(types: NotificationType[]): void {
        Notifications.#instance.clear(types);
    }

    /**
     * Renders a red error notification message.
     * @param msg               The message to be displayed.
     * @param durationInSeconds For how many seconds should the message be displayed (min set during initialization)? 
     *                          If omitted, the message persists until closed by the user.
     */ 
    static error(msg: string, durationInSeconds?: number): void {
        Notifications.#instance.notify("error", msg, durationInSeconds);
    }

    /**
     * Renders a yellow warning notification message.
     * @param msg               The message to be displayed.
     * @param durationInSeconds For how many seconds should the message be displayed (min set during initialization)? 
     *                          If omitted, the message persists until closed by the user.
     */ 
    static warn(msg: string, durationInSeconds?: number): void {
        Notifications.#instance.notify("warn", msg, durationInSeconds);
    }

    /**
     * Renders a green success notification message.
     * @param msg               The message to be displayed.
     * @param durationInSeconds For how many seconds should the message be displayed (min set during initialization)? 
     *                          If omitted, the message persists until closed by the user.
     */ 
    static success(msg: string, durationInSeconds?: number): void {
        Notifications.#instance.notify("success", msg, durationInSeconds);
    }

    /**
     * Renders a blue info notification message.
     * @param msg               The message to be displayed.
     * @param durationInSeconds For how many seconds should the message be displayed (min set during initialization)? 
     *                          If omitted, the message persists until closed by the user.
     */ 
    static info(msg: string, durationInSeconds?: number): void {
        Notifications.#instance.notify("info", msg, durationInSeconds);
    }

    //#endregion

    #slots: NotificationSlot[] = [];
    #queue: NotificationData[] = [];
    #debounceTimeoutId: number | null = null;
    #renderTimeoutId: number | null = null;
    #config: NotificationsConfig;
    #container: HTMLElement;

    private constructor(cfg: Partial<NotificationsConfig> = {}, ) {
        this.#config = { ...Notifications.DEFAULT_CONFIG, ...cfg };

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

    /**
     * Renders a notification message of a given type.
     * @param type      The type of the notification.
     * @param msg       The message to be displayed.
     * @param durationInSeconds For how many seconds should the message be displayed (min set during initialization)? 
     *                          If omitted, the message persists until closed by the user.
     */ 
    notify(type: NotificationType, msg: string, durationInSeconds?: number): void {
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
    clear(types?: NotificationType[]) {
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
