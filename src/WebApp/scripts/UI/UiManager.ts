import { IPixiHandler } from "./PixiHandler/PixiHandler";
import { IDataViews, BodyFrameData, SimFrameData, type BodyView } from "../Data/DataViews";
import { IViewModelMovable } from "./abstract/ViewModelMovable";
import { INotifications } from "./Notifications/Notifications";
import { IController } from "../Controller/Controller";
import TimeControls from "./components/TimeControls/TimeControls";
import { IViewModel } from "./abstract/ViewModel";

/** Owner and orchestrator of UI components. */
export interface IUiManager {
    /**
     * Initializes the individual UI components managed by the UIManager.
     * @param controller The central controller to be injected into components. 
     */
    injectController(controller: IController): void;
    /**
     * Updates UI components based on the provided data views.
     * @param views The processed and validated data views for the frame to render.
     */
    render(views: IDataViews): void;
}

export default class UiManager implements IUiManager {
    #controller!: IController;

    // Permanent UI components
    #pixi: IPixiHandler;
    #notif: INotifications;
    #timeControls!: IViewModel; // initialized late

    /** Map of temporary UI components. */
    #temp: Map<IViewModelMovable["id"], IViewModelMovable> = new Map();
    /** Simple counter for throttled updated. */
    #frameCounter: number = 0;

    constructor(pixi: IPixiHandler, notif: INotifications) {
        this.#pixi = pixi;
        this.#notif = notif;
    }

    injectController(controller: IController) {
        this.#controller = controller;
    }

    render(views: IDataViews): void {
        if(this.#frameCounter === 0) this.#onFirstRender(views);
        
        this.#renderSim(views.simFrameData);
        this.#renderBodies(views.bodyFrameData);

        this.#frameCounter++;
    }

    #onFirstRender(views: IDataViews) {
        this.#timeControls ??= new TimeControls("time-controls", this.#controller, views.simView);
    }

    #renderSim(frameData: SimFrameData): void {
        this.#timeControls.render(frameData);
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