/**
 * @typedef {object} BodyMetaData
 * @property {string} name
 * @property {string|number} tint
 */
export default class AppDataManager {
    /** @type {BodyMetaData} */
    static DEFAULT_BODY_DATA: BodyMetaData;
    /** @type {Map<number, BodyMetaData>} */
    bodyData: Map<number, BodyMetaData>;
    /**
     *
     * @param {number} id
     */
    _onCreateBody(id: number): void;
    _onDeleteBody(id: any): void;
    _onUpdateBody(id: any, updates?: {}): boolean;
    /**
     *
     * @returns {string}
     */
    getPreset(): string;
    /**
     * Loads a given preset string.
     * @param {string} presetString
     * @param {boolean} [preserveState=true]    Should the current state be preserved and restored in case of an error?
     *                                          If not, the error is re-thrown.
     * @returns {void}
     */
    loadPreset(presetString: string, preserveState?: boolean): void;
}
export type BodyMetaData = {
    name: string;
    tint: string | number;
};
