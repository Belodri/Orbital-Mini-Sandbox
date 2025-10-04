import PixiHandler from "./PixiHandler";
import UiData, { type BodyView } from "./UiData";
import type { ViewModel, ViewModelMovable } from "./ViewModel";

const UI_MANAGER_CONFIG_DEFAULTS: UiManagerConfig = {
    isDebug: false,
} as const;

export type UiManagerConfig = {
    isDebug: boolean
}

/**
 * Static orchestrator of UI components, and owner and manager of all non-static UI components.
 * 
 * Assumes that the following static dependencies are initialized before the first {@link UiManager.render} call:
 * - {@link UiData}
 * - {@link PixiHandler}
 */
export default class UiManager {
    static #instanceField: UiManager;
    static get #instance() { 
        if(!UiManager.#instanceField) throw new Error("UiManager has not been initialized.");
        return UiManager.#instanceField;
    }

    //#region Public API

    /**
     * Initializes the class. Calling any other method before this one will throw an Error.
     * Repeated initialization calls are safely ignored.
     * @param config Partial configuration data. Is merged with and overrides default config.
     */
    static init(config: Partial<UiManagerConfig> = {}): void {
        UiManager.#instanceField ??= new UiManager({
            ...UI_MANAGER_CONFIG_DEFAULTS,
            ...config
        });
    }

    /**
     * Updates UI components based on the frameData provided by {@link UiData}.
     * Assumes all data is valid, as per `UiData`'s contract!
     */
    static render(): void { UiManager.#instance.#render(); }

    //#endregion

    /** Record of permanent UI components. */
    #perm: Record<ViewModel["id"], ViewModel> = {};
    /** Map of temporary UI components. */
    #temp: Map<ViewModelMovable["id"], ViewModelMovable> = new Map();
    /** Simple counter for throttled updated. */
    #frameCounter: number = 0;
    #config: UiManagerConfig;

    private constructor(config: UiManagerConfig) {
        this.#config = config;
    }

    #render(): void {
        this.#renderSim();
        this.#renderBodies();

        this.#frameCounter++;
    }

    #renderSim(): void {
        
    }

    #renderBodies(): void {
        const frameData = UiData.bodyFrameData;
        for(const view of frameData.created) this.#renderCreatedBody(view);
        for(const id of frameData.deleted) this.#renderDeletedBody(id);
        for(const view of frameData.updated) this.#renderUpdatedBody(view);
    }

    #renderCreatedBody(view: BodyView): void {
        PixiHandler.onCreateBody(view);
    }

    #renderDeletedBody(id: BodyView["id"]): void {
        PixiHandler.onDeleteBody(id);
    }

    #renderUpdatedBody(view: BodyView): void {
        PixiHandler.onUpdateBody(view.id);
    }
}