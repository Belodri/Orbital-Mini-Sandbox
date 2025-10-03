import { BodyId, BodyState, SimState, StateData, DiffData as PhysicsDiff } from "@bridge";
import { AppStateBody, AppStateSim, AppState, AppDiff } from "../AppData";

export interface BodyView {
    readonly id: BodyId,
    readonly app: Readonly<AppStateBody>;
    readonly physics: Readonly<BodyState>;
}

export interface SimView {
    readonly app: Readonly<AppStateSim>;
    readonly physics: Readonly<SimState>;
}

type DataViewsInjections = {
    physicsState: StateData;
    physicsDiff: PhysicsDiff; 
    appState: AppState;
    appDiff: AppDiff;
}

/**
 * Transient data object containing the BodyViews and BodyIds 
 * of bodies that were created/updated/deleted during the last frame.
 */
export type BodyFrameData = {
    /** The BodyView objects of bodies created this frame. */
    readonly created: readonly BodyView[];
    /** The BodyView objects of bodies with updated app data this frame. */
    readonly updatedApp: readonly BodyView[];
    /** The BodyView objects of bodies with updated physics data this frame. */
    readonly updatedPhysics: readonly BodyView[],
    /** The BodyIds of bodies deleted this frame. */
    readonly deleted: ReadonlySet<BodyId>;
}

/**
 * Transient data object containing the keys of SimView properties
 * that were updated during the last frame.
 */
export type SimFrameData = {
    readonly app: Readonly<AppDiff["sim"]>,
    readonly physics: Readonly<PhysicsDiff["sim"]>
}

export class UiDataValidationError extends Error {
    constructor(message: string) {
        super(message);
        this.name = "UiDataValidationError";
    }
}

/**
 * Static data provider for UI components
 */
export default class UiData {
    static #instanceField: UiData;

    static get #instance() { 
        if(!UiData.#instanceField) throw new Error("UiData has not been initialized.");
        return UiData.#instanceField;
    }

    //#region Public API

    /**
     * Initializes the class. Calling any other method before this one will throw an Error.
     * Repeated initialization calls are safely ignored.
     * @param injections 
     */
    static init(injections: DataViewsInjections) {
        UiData.#instanceField ??= new UiData(injections)
    }

    /** The SimView object holding references to live data. */
    static get simView(): SimView { return UiData.#instance.#simView; }

    /** A map of BodyIds to BodyView objects holding references to live data. */
    static get bodyViews(): Map<BodyId, BodyView> { return UiData.#instance.#bodyViews; }

    /** Transient, prepared data of simulation property keys that were updated this frame. */
    static get simFrameData(): SimFrameData { return UiData.#instance.#simFrameData; }
    
    /** Transient, prepared data of bodies that were created, updated, or deleted this frame. */
    static get bodyFrameData(): BodyFrameData { return UiData.#instance.#bodyFrameData; }

    /**
     * Processes and refreshes the data views and frameData by reading from the injected sources.
     * 
     * Any issue or inconsistency with the data from the injected sources will throw an error.
     * Calling code is responsible for handling these!
     * @throws {UiDataValidationError}
     */
    static refresh(): void { UiData.#instance.#refresh(); }

    /** 
     * Resets the singleton instance for testing.
     * @internal
     */
    static _reset(): void { UiData.#instanceField = undefined as any; }

    //#endregion

    #simView: SimView;
    #bodyViews: Map<BodyId, BodyView> = new Map();

    #physicsState: StateData;
    #physicsDiff: PhysicsDiff;
    #appState: AppState;
    #appDiff: AppDiff;

    #simFrameData: SimFrameData;
    #bodyFrameData: BodyFrameData;

    #bodyCreated: BodyView[] = [];
    #bodyUpdatedApp: BodyView[] = [];
    #bodyUpdatedPhysics: BodyView[] = [];

    private constructor(injections: DataViewsInjections) {
        this.#physicsState = injections.physicsState;
        this.#physicsDiff = injections.physicsDiff;
        this.#appState = injections.appState;
        this.#appDiff = injections.appDiff;

        this.#simView = {
            app: this.#appState.sim,
            physics: this.#physicsState.sim
        };

        this.#simFrameData = {
            app: this.#appDiff.sim,
            physics: this.#physicsDiff.sim
        };

        this.#bodyFrameData = {
            created: this.#bodyCreated,
            updatedApp: this.#bodyUpdatedApp,
            updatedPhysics: this.#bodyUpdatedPhysics,
            deleted: this.#appDiff.bodies.deleted,
        }
    }

    #refresh(): void {
        // Validate before refresh methods as those rely on valid data!
        this.#validateDiffs();

        // After validation it doesn't matter in which order these are run.
        this.#refreshCreatedBodies();
        this.#refreshUpdatedBodies("app");
        this.#refreshUpdatedBodies("physics");
        this.#refreshDeletedBodies();
    }


    #validateDiffs() {
        const equal = (a: Set<number>, b: Set<number>) => a.size === b.size && a.isSubsetOf(b);

        const app = this.#appDiff.bodies;
        const physics = this.#physicsDiff.bodies;

        // Note: Be careful to not break the sequential cohesion of these checks when refactoring!

        // Validate - Created
        if(!equal(physics.created, app.created)) throw new UiDataValidationError("Invalid diff: Created body diffs out of sync.");
        if(!app.created.isSubsetOf(this.#appState.bodies)) throw new UiDataValidationError("Invalid diff: Not all created bodies have an AppState.");
        if(!physics.created.isSubsetOf(this.#physicsState.bodies)) throw new UiDataValidationError("Invalid diff: Not all created bodies have a PhysicsState.");

        // Validate - Deleted
        if(!equal(app.deleted, physics.deleted)) throw new UiDataValidationError("Invalid diff: Deleted body diffs out of sync.");
        if(!app.deleted.isDisjointFrom(app.created)) throw new UiDataValidationError("Invalid diff: Overlap between created and deleted body diffs detected.");
        if(!app.deleted.isSubsetOf(this.#bodyViews)) throw new UiDataValidationError("Invalid diff: Not all deleted bodies have a DataView.");  

        // Validate - Updated (App)
        if(!app.updated.isDisjointFrom(app.created)) throw new UiDataValidationError("Invalid diff: Overlap between created and updated(app) body diffs detected.");
        if(!app.updated.isDisjointFrom(app.deleted)) throw new UiDataValidationError("Invalid diff: Overlap between deleted and updated(app) body diffs detected.");
        if(!app.updated.isSubsetOf(this.#bodyViews)) throw new UiDataValidationError("Invalid diff: Not all updated(app) bodies have a DataView.");

        // Updated - Physics
        if(!physics.updated.isDisjointFrom(physics.created)) throw new UiDataValidationError("Invalid diff: Overlap between created and updated(physics) body diffs detected.");
        if(!physics.updated.isDisjointFrom(physics.deleted)) throw new UiDataValidationError("Invalid diff: Overlap between deleted and updated(physics) body diffs detected.");
        if(!physics.updated.isSubsetOf(this.#bodyViews)) throw new UiDataValidationError("Invalid diff: Not all updated(physics) bodies have a DataView.");
    }


    #refreshCreatedBodies() {
        this.#bodyCreated.length = 0;

        for(const id of this.#appDiff.bodies.created) {
            // Non-null assertions safe because of prior validation in #validateDiffs()
            const app = this.#appState.bodies.get(id)!;
            const physics = this.#physicsState.bodies.get(id)!;

            const bodyView = {id, app, physics};
            this.#bodyViews.set(id, bodyView);
            this.#bodyCreated.push(bodyView);
        }
    }

    #refreshUpdatedBodies(type: "app" | "physics") {
        const viewArray = type === "app" ? this.#bodyUpdatedApp : this.#bodyUpdatedPhysics;
        const diff = type === "app" ? this.#appDiff.bodies.updated : this.#physicsDiff.bodies.updated;

        viewArray.length = 0;

        for(const id of diff) {
            // Non-null assertion safe because of prior validation in #validateDiffs()
            const bodyView = this.#bodyViews.get(id)!;
            viewArray.push(bodyView);
        }
    }

    #refreshDeletedBodies() {
        for(const id of this.#appDiff.bodies.deleted) this.#bodyViews.delete(id);

        // `this.#bodyFrameData.deleted` is set directly in the constructor and directly 
        // references `this.#appDiff.bodies.deleted`. As long as validation passes, this
        // is always correct for the current frame.
    }
}
