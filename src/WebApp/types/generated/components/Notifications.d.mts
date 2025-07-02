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
    /**
     * @param {Partial<NotificationsInitConfig>} [config={}]
     * @param {Partial<NotificationConfig>} [notifConfigDefaults={}]
     */
    constructor(config?: Partial<NotificationsInitConfig>, notifConfigDefaults?: Partial<NotificationConfig>);
    /**
     * Adds a new message to the notifications queue and schedules a render.
     * @param {string} message
     * @param {Partial<NotificationConfig>} [config]
     */
    add(message: string, config?: Partial<NotificationConfig>): void;
    #private;
}
export type NotificationsInitConfig = {
    containerId: string;
    /**
     * The maximum number of elements
     */
    maxElements: number;
    /**
     * The css classes for the notification elements.
     */
    notifEleClassList: string[];
    /**
     * The minimum interval between renders in ms.
     */
    minRenderInterval: number;
    /**
     * The debounce delay for debounceRender calls.
     */
    renderDebounceMs: number;
};
export type NotificationConfig = {
    /**
     * The minimum display duration in ms
     */
    minDurationMs: number;
};
