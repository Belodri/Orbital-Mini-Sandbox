import { BodyId, BodyState, SimState, StateData, DiffData as PhysicsDiff } from "@bridge";
import { BodyData, SimData, AppData, AppDataDiff } from "./AppDataStore";

export interface DataViewBody {
    app: Readonly<BodyData>;
    physics: Readonly<BodyState>
}

export interface DataViewSim {
    app: Readonly<SimData>;
    physics: Readonly<SimState>
}

export default class UiManager {
    #simView: DataViewSim;
    #bodyViews: Map<BodyId, DataViewBody> = new Map();

    #physicsState: StateData;
    #physicsDiff: PhysicsDiff;
    #appData: AppData;
    #appDiff: AppDataDiff;

    #ui = {
        temp: new Map(),    // store for transient, short-lived ui components
        perm: {}            // references to permanent ui components
    }

    constructor(physicsStateRef: StateData, physicsDiffRef: PhysicsDiff, appDataRef: AppData, appDataDiffRef: AppDataDiff) {
        this.#physicsState = physicsStateRef;
        this.#physicsDiff = physicsDiffRef;
        this.#appData = appDataRef;
        this.#appDiff = appDataDiffRef;

        this.#simView = {
            app: appDataRef.sim,
            physics: physicsStateRef.sim
        }
    }

    render() {
        this.#renderBodies();
        this.#renderSim();
    }

    #renderSim() {
        // ...component updates
    }

    #renderBodies() {
        if(this.#appDiff.bodies.created.size !== this.#physicsDiff.bodies.created.size) throw new Error("Created body diffs out of sync.");
        if(this.#appDiff.bodies.deleted.size !== this.#physicsDiff.bodies.deleted.size) throw new Error("Deleted body diffs out of sync.");

        const created = this.#appDiff.bodies.created;
        const deleted = this.#appDiff.bodies.deleted;
        const updatedPhysics = this.#physicsDiff.bodies.updated;
        const updatedApp = this.#appDiff.bodies.updated;

        for(const id of deleted) this.#onDeletedBody(id);

        for(const [id, bodyState] of this.#physicsState.bodies) {
            if(created.has(id)) {
                const appData = this.#appData.bodies.get(id);
                if(!appData) throw new Error(`No appData for body id: ${id}`);
                this.#onCreatedBody(bodyState, appData);
                continue;
            }

            const physicsUpdated = updatedPhysics.has(id);
            const appUpdated = updatedApp.has(id);
            if(!physicsUpdated && !appUpdated) continue;

            const view = this.#bodyViews.get(id);
            if(!view) throw new Error(`No view for body id: ${id}`);
            this.#onUpdateBody(view, physicsUpdated, appUpdated);
        }
        
    }

    #onDeletedBody(id: BodyId) {
        this.#bodyViews.delete(id);
        // ...component updates
    }

    #onCreatedBody(physics: BodyState, app: BodyData) {
        this.#bodyViews.set(physics.id, { app, physics });
        // ...component updates
    }

    #onUpdateBody(view: DataViewBody, physicsUpdated: boolean, appUpdated: boolean) {
        // ...component updates
    }
}