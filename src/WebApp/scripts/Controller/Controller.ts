import { IBridge, BodyId } from "@bridge";
import { IAppData } from "../Data/AppData";
import { INotifications } from "../UI/Notifications/Notifications";
import { IDeferredResolver } from "../utils/DeferredResolver";
import { IPixiHandler } from "../UI/PixiHandler/PixiHandler";

export interface IController {
    /**
     * Toggles or sets the paused state of the simulation.
     * Code is executed immediately and can throw synchronous errors!
     * @param force     If provided, sets the paused state directly (`true` for paused, `false` for running). If omitted, the state is toggled.
     */
    togglePaused(force?: boolean): Promise<void>;
    /**
     * Creates a new body in the simulation.  
     * Code is executed immediately and can throw synchronous errors!
     * @returns         A promise that resolves with the unique ID of the new body at the start of the next post-render phase.
     */
    createBody(): Promise<BodyId>;
    /**
     * Deletes a body from the simulation.  
     * Code is executed immediately and can throw synchronous errors!
     * @param id        The unique ID of the body to delete.
     * @returns         A promise that resolves resolves at the start of the next post-render phase. `true` if the body was deleted, or `false` if it wasn't found.
     */
    deleteBody(id: BodyId): Promise<boolean>;
    /**
     * Updates the simulation's app data or physics data.  
     * Code is executed immediately and can throw synchronous errors!
     * @param updates   Partial update data.
     * @returns         A promise that resolves at the start of the next post-render phase.
     */
    updateSimulation(updates: { physics: Parameters<IBridge["updateSimulation"]>[0] }): Promise<void>;
    updateSimulation(updates: { app: Parameters<IAppData["updateSimulationData"]>[0] }): Promise<void>;
    updateSimulation(updates: { app: Parameters<IAppData["updateSimulationData"]>[0] } | { physics: Parameters<IBridge["updateSimulation"]>[0] }): Promise<void>;
    /**
     * Updates a specific body's app data or physics data.
     * This code is executed immediately and can throw synchronous errors!
     * @param id The unique ID of the body to update.
     * @param updates An object containing either `app` or `physics` data for the update.
     * @returns A promise that resolves with a boolean indicating if the update was successful (e.g., if the body exists).
     */
    updateBody(id: BodyId, updates: { physics: Parameters<IBridge["updateBody"]>[1] }): Promise<boolean>;
    updateBody(id: BodyId, updates: { app: Parameters<IAppData["updateBodyData"]>[1] }): Promise<boolean>;
    updateBody(id: BodyId, updates: { app: Parameters<IAppData["updateBodyData"]>[1] } | { physics: Parameters<IBridge["updateBody"]>[1] }): Promise<boolean>;
}

export default class Controller implements IController {
    #appData: IAppData;
    #bridge: IBridge;
    #notif: INotifications;
    #resolver: IDeferredResolver;
    #pixi: IPixiHandler;

    constructor(appData: IAppData, bridge: IBridge, notif: INotifications, resolver: IDeferredResolver, pixi: IPixiHandler) {
        this.#appData = appData;
        this.#bridge = bridge;
        this.#notif = notif;
        this.#resolver = resolver;
        this.#pixi = pixi;

        // Bind overloaded methods
        this.updateBody = this.updateBody.bind(this);
        this.updateSimulation = this.updateSimulation.bind(this);
    }

    togglePaused = (force?: boolean) => {
        const curr = this.#appData.state.sim.paused;
        const newState = typeof force === "boolean" ? force : !curr;
        return this.updateSimulation({app: { paused: newState }});
    }

    createBody = () => this.#resolver.execute(() => this.#bridge.createBody());

    deleteBody = (id: BodyId) => this.#resolver.execute(() => this.#bridge.deleteBody(id));

    updateSimulation(updates: { physics: Parameters<IBridge["updateSimulation"]>[0]; }): Promise<void>;
    updateSimulation(updates: { app: Parameters<IAppData["updateSimulationData"]>[0]; }): Promise<void>;
    updateSimulation(updates: { app: Parameters<IAppData["updateSimulationData"]>[0]; } | { physics: Parameters<IBridge["updateSimulation"]>[0]; }): Promise<void> {
        return this.#resolver.execute(() => "app" in updates 
            ? this.#appData.updateSimulationData(updates.app)
            : this.#bridge.updateSimulation(updates.physics)
        );
    }

    updateBody(id: BodyId, updates: { physics: Parameters<IBridge["updateBody"]>[1]; }): Promise<boolean>;
    updateBody(id: BodyId, updates: { app: Parameters<IAppData["updateBodyData"]>[1]; }): Promise<boolean>;
    updateBody(id: BodyId, updates: { app: Parameters<IAppData["updateBodyData"]>[1]; } | { physics: Parameters<IBridge["updateBody"]>[1]; }): Promise<boolean> {
        return this.#resolver.execute(() => "app" in updates
            ? this.#appData.updateBodyData(id, updates.app)
            : this.#bridge.updateBody(id, updates.physics)
        );
    }
}
