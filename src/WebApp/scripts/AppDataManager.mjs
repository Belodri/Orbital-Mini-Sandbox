import AppShell from "./AppShell.mjs";

/**
 * @typedef {object} BodyMetaData
 * @property {string} name
 */

export default class AppDataManager {
    /** @type {BodyMetaData} */
    static DEFAULT_BODY_DATA = {
        name: "New Body",
    }

    /** @type {Map<number, BodyMetaData>} */
    bodyData = new Map();

    /**
     * 
     * @param {number} id 
     */
    _onBodyCreated(id) {
        this.bodyData.set(id, { ...AppDataManager.DEFAULT_BODY_DATA });
    }

    getPreset() {
        const data = {
            bodyData: [...this.bodyData.entries()],     // this.bodyData is a Map<number, object>
            simDataStr: AppShell.Bridge.getPreset()         // returns a JSON formatted string
        };
        return JSON.stringify(data);
    }

    /**
     * Loads a given preset string.
     * @param {string} presetString 
     * @param {boolean} [preserveState=true]    Should the current state be preserved and restored in case of an error?
     *                                          If not, the error is re-thrown.
     * @returns {void}                       
     */
    loadPreset(presetString, preserveState = true) {
        AppShell.togglePause(true);
        const prevState = preserveState ? this.getPreset() : "";

        try {
            const {bodyData, simDataStr} = JSON.parse(presetString, (k, v) => {
                if(k === "bodyData") return new Map(v);
                else return v;
            });

            AppShell.Bridge.loadPreset(simDataStr); // throws if JSON is invalid

            // Verify bodyData

            if(AppShell.Bridge.simState.bodies.size !== bodyData.size)
                throw new Error(`Invalid Preset: Mismatch between simulation data and body metadata.`);

            const simStateKeys = new Set(AppShell.Bridge.simState.bodies.keys());
            const bodyDataKeys = new Set(bodyData.keys());
            if(!simStateKeys.isSubsetOf(bodyDataKeys)) 
                throw new Error(`Invalid Preset: Mismatch between simulation data and body metadata.`);

            this.bodyData = bodyData;
        } catch(err) {
            AppShell.notifications.add(`Invalid Preset`);
            if(prevState) return this.loadPreset(prevState, false);
            else throw new Error(`Invalid Preset Error`, {cause: err});
        }
    }
}
