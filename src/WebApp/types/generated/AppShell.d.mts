/**
 * @import { BodyStateData } from '../types/Bridge'
 * @import { BodyMetaData } from './AppDataManager.mjs'
 */
export default class AppShell {
    static "__#4@#CONFIG": {
        debugMode: boolean;
    };
    /** @type {typeof import("../types/Bridge").default} */
    static Bridge: typeof import("../types/Bridge").default;
    /** @type {Notifications} */
    static notifications: Notifications;
    /** @type {AppDataManager} */
    static appDataManager: AppDataManager;
    /** @type {CanvasView} */
    static canvasView: CanvasView;
    static initialize(): Promise<void>;
    /**
     *
     * @returns {number}
     */
    static createBody(): number;
    /**
     *
     * @param {number} id
     * @returns {boolean}
     */
    static deleteBody(id: number): boolean;
    /**
     *
     * @param {number} id
     * @param {Partial<BodyStateData & BodyMetaData>} updates
     */
    static updateBody(id: number, updates?: Partial<BodyStateData & BodyMetaData>): boolean;
    static stopLoop(): void;
    static startLoop(): void;
    /**
     *
     * @param {string} msg
     * @param {any} data
     * @returns {void}
     */
    static log(msg: string, data: any): void;
}
import Notifications from './components/Notifications.mjs';
import AppDataManager from './AppDataManager.mjs';
import CanvasView from './components/CanvasView.mjs';
import type { BodyStateData } from '../types/Bridge';
import type { BodyMetaData } from './AppDataManager.mjs';
