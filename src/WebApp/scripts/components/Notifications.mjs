/**
 * @typedef {object} NotificationsInitConfig
 * @property {string} containerId
 * @property {number} maxElements           The maximum number of elements
 * @property {string[]} notifEleClassList   The css classes for the notification elements.
 * @property {number} minRenderInterval     The minimum interval between renders in ms.
 * @property {number} renderDebounceMs      The debounce delay for debounceRender calls.
 */

/**
 * @typedef {object} NotificationConfig
 * @property {number} minDurationMs         The minimum display duration in ms
 */


export default class Notifications {
    static Z_INDEX_MIN = Number.MAX_SAFE_INTEGER / 2;

    /**
     * @typedef {{message: string} & NotificationConfig } QueueItem
     */

    /** @type {QueueItem[]} */
    #queue = [];

    /** @type {{element: HTMLElement, endTs: number}[]} */
    #rendered = [];

    /** @type {HTMLDivElement} */
    #container;

    /** @type {number | null} */
    #debounceTimeoutId = null;

    /** @type {number | null} */
    #renderTimeoutId = null;

    /** @type {NotificationsInitConfig} */
    #config = {
        containerId: "notifications-container", 
        maxElements: 3,
        notifEleClassList: [
            "notification-message"
        ],
        minRenderInterval: 1000,
        renderDebounceMs: 100,
    }

    /** @type {NotificationConfig} */
    #notifConfigDefaults = {
        minDurationMs: 10 * 1000,
    }

    /**
     * @param {Partial<NotificationsInitConfig>} [config={}]
     * @param {Partial<NotificationConfig>} [notifConfigDefaults={}] 
     */
    constructor(config = {}, notifConfigDefaults={}) {
        this.#config = {...this.#config, ...config}
        this.#notifConfigDefaults = {...this.#notifConfigDefaults, ...notifConfigDefaults};

        const container =  document.getElementById(this.#config.containerId);
        if(!container || !(container instanceof HTMLDivElement)) 
            throw new Error(`Invalid container with id '${this.#config.containerId}'.`);
        container.style.zIndex = String(Notifications.Z_INDEX_MIN);

        this.#container = container;
    }

    /**
     * Adds a new message to the notifications queue and schedules a render.
     * @param {string} message 
     * @param {Partial<NotificationConfig>} [config] 
     */
    add(message, config = {}) {
        const queueItem = {
            message,
            ...this.#notifConfigDefaults,
            ...config
        };

        this.#queue.push(queueItem);
        this.#triggerRender();
    }

    /**
     * The main render loop which cleans up, renders new items, and schedules the next run.
     */
    #render() {
        if(this.#renderTimeoutId) clearTimeout(this.#renderTimeoutId);

        const now = Date.now();
        this.#removeExpiredNotifications(now);
        this.#renderQueuedNotifications(now);
        const nextRenderDelay = this.#getNextRenderDelay(now);

        this.#renderTimeoutId = nextRenderDelay
            ? setTimeout(() => this.#render(), nextRenderDelay)
            : null;
    }

    /**
     * Debounces calls to the main render loop.
     */
    #triggerRender() {
        clearTimeout(this.#debounceTimeoutId)
        this.#debounceTimeoutId = setTimeout(() => {
            this.#debounceTimeoutId = null;
            this.#render();
        }, this.#config.renderDebounceMs);
    }

    /**
     * Calculate the delay for the next rerender.
     * @param {number} now          The current timestamp to check against.
     * @returns {number | null}     In how many ms the next rerender should happen, or null if none are scheduled.
     */
    #getNextRenderDelay(now) {
        if (this.#rendered.length === 0) return null;

        let nextTs = Infinity;
        for(const {endTs} of this.#rendered) {
            if(nextTs > endTs) nextTs = endTs;
        }

        const delay = nextTs - now;
        return Math.max(delay, this.#config.minRenderInterval);
    }

    /**
     * Removes any expired notifications from the DOM and the rendered list.
     * @param {number} now          The current timestamp to check against.
     * @returns {void}
     */
    #removeExpiredNotifications(now) {
        for(let i = this.#rendered.length - 1; i >= 0; i--) {
            const {element, endTs} = this.#rendered[i];
            const endsIn = endTs - now;
            if(endsIn <= 0) {
                this.#rendered.splice(i, 1);
                element.remove();
            }
        }
    }

    /**
     * Takes queued notifications and renders them if there are available slots.
     * @param {number} now          The current timestamp to check against.
     * @returns {void}
     */
    #renderQueuedNotifications(now) {
        const availableSlots = this.#config.maxElements - this.#rendered.length;
        if (availableSlots <= 0) return;

        const itemsToRender = this.#queue.splice(0, availableSlots);
        for(const notifData of itemsToRender) {
            const element = this.#createNotifEle(notifData);
            const endTs = now + notifData.minDurationMs;
            this.#rendered.push({element, endTs});
        }
    }

    /**
     * Creates a notification element and appends it into the container.
     * @param {QueueItem} queueItem 
     * @returns {HTMLElement}
     */
    #createNotifEle(queueItem) {
        const ele = document.createElement("span");
        ele.classList.add(...this.#config.notifEleClassList);
        ele.textContent = queueItem.message;
        this.#container.appendChild(ele);
        return ele;
    }
}