import { IPixiHandler } from "./PixiHandler/PixiHandler";
import { IDataViews, BodyFrameData, SimFrameData, type BodyView } from "../Data/DataViews";
import type { ViewModelMovable } from "./abstract/ViewModelMovable";
import { INotifications } from "./Notifications/Notifications";


/** Owner and orchestrator of UI components. */
export interface IUiManager {
    /**
     * Updates UI components based on the provided data views.
     * @param views The processed and validated data views for the frame to render.
     */
    render(views: IDataViews): void;
}

export default class UiManager implements IUiManager {
    // Permanent UI components
    #pixi: IPixiHandler;
    #notif: INotifications;

    /** Map of temporary UI components. */
    #temp: Map<ViewModelMovable["id"], ViewModelMovable> = new Map();
    /** Simple counter for throttled updated. */
    #frameCounter: number = 0;

    constructor(pixi: IPixiHandler, notif: INotifications) {
        this.#pixi = pixi;
        this.#notif = notif;
    }

    render(views: IDataViews): void {
        this.#renderSim(views.simFrameData);
        this.#renderBodies(views.bodyFrameData);

        this.#frameCounter++;
    }

    #renderSim(frameData: SimFrameData): void {
        
    }

    #renderBodies(frameData: BodyFrameData): void {
        for(const view of frameData.created) this.#renderCreatedBody(view);
        for(const id of frameData.deleted) this.#renderDeletedBody(id);
        for(const view of frameData.updated) this.#renderUpdatedBody(view);
    }

    #renderCreatedBody(view: BodyView): void {
        this.#pixi.onCreateBody(view);
    }

    #renderDeletedBody(id: BodyView["id"]): void {
        this.#pixi.onDeleteBody(id);
    }

    #renderUpdatedBody(view: BodyView): void {
        this.#pixi.onUpdateBody(view.id);
    }
}