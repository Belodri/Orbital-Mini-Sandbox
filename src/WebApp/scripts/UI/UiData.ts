import { BodyId, PhysicsStateBody, PhysicsStateSim, PhysicsState, PhysicsDiff } from "@bridge";
import { AppStateBody, AppStateSim, AppState, AppDiff } from "../AppData";

export interface BodyView {
    readonly id: BodyId,
    readonly app: Readonly<AppStateBody>;
    readonly physics: Readonly<PhysicsStateBody>;
}

export interface SimView {
    readonly app: Readonly<AppStateSim>;
    readonly physics: Readonly<PhysicsStateSim>;
}

type DataViewsInjections = {
    physicsState: PhysicsState;
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
    /** 
     * The BodyView objects of bodies with updated physics or app data this frame.
     * If a component really needs to know whether the update was app or physics, 
     * it can use {@link UiData.appDiff.bodies.updated} or 
     * {@link UiData.physicsDiff.bodies.updated} respectively.
     */
    readonly updated: readonly BodyView[];
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

    /** Contains the direct diff information about physics data changes during the last engine tick. */
    static get physicsDiff(): PhysicsDiff { return UiData.#instance.#physicsDiff; }

    /** Contains the direct diff information about app data changes during the last engine tick. */
    static get appDiff(): AppDiff { return UiData.#instance.#appDiff; }

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

    #physicsState: PhysicsState;
    #physicsDiff: PhysicsDiff;
    #appState: AppState;
    #appDiff: AppDiff;

    #simFrameData: SimFrameData;
    #bodyFrameData: BodyFrameData;

    #bodyCreated: BodyView[] = [];
    #bodyUpdated: BodyView[] = [];

    #bodyUpdatedDiffUnion: Set<BodyId> = new Set();

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
            updated: this.#bodyUpdated,
            deleted: this.#physicsDiff.bodies.deleted,
        }
    }

    #refresh(): void {
        // Create a union of updated body diffs.

        // Set.prototype.union() is faster but creates a new Set every frame.
        // TODO: Benchmark if Set.prototype.union() is overall faster than this.

        this.#bodyUpdatedDiffUnion.clear();
        for(const id of this.#physicsDiff.bodies.updated) this.#bodyUpdatedDiffUnion.add(id);
        for(const id of this.#appDiff.updatedBodies) this.#bodyUpdatedDiffUnion.add(id);

        // Validate before refresh methods as those rely on valid data!
        this.#validateDiffs();

        // After validation it doesn't matter in which order these are run.
        this.#refreshCreatedBodies();
        this.#refreshUpdatedBodies();
        this.#refreshDeletedBodies();
    }


    #validateDiffs() {
        const app = this.#appDiff.updatedBodies;
        const physics = this.#physicsDiff.bodies;

        // Note: Be careful to not break the sequential cohesion of these checks when refactoring!

        // Validate - Created
        if(!physics.created.isSubsetOf(this.#physicsState.bodies)) throw new UiDataValidationError("Invalid diff: Not all created bodies have a PhysicsState.");

        // Validate - Deleted
        if(!physics.deleted.isDisjointFrom(physics.created)) throw new UiDataValidationError("Invalid diff: Overlap between created and deleted body diffs detected.");
        if(!physics.deleted.isSubsetOf(this.#bodyViews)) throw new UiDataValidationError("Invalid diff: Not all deleted bodies have a DataView.");  

        // Validate - Updated (individual)
        if(!app.isDisjointFrom(physics.created)) throw new UiDataValidationError("Invalid diff: Overlap between created and updated(app) body diffs detected.");
        if(!app.isDisjointFrom(physics.deleted)) throw new UiDataValidationError("Invalid diff: Overlap between deleted and updated(app) body diffs detected.");
        if(!physics.updated.isDisjointFrom(physics.created)) throw new UiDataValidationError("Invalid diff: Overlap between created and updated(physics) body diffs detected.");
        if(!physics.updated.isDisjointFrom(physics.deleted)) throw new UiDataValidationError("Invalid diff: Overlap between deleted and updated(physics) body diffs detected.");

        // Validate - Updated (union)
        if(!this.#bodyUpdatedDiffUnion.isSubsetOf(this.#bodyViews)) throw new UiDataValidationError("Invalid diff: Not all updated bodies have a DataView.");
    }


    #refreshCreatedBodies() {
        this.#bodyCreated.length = 0;

        for(const id of this.#physicsDiff.bodies.created) {
            // Non-null assertions are safe because of prior validation in #validateDiffs()
            const app = this.#appState.bodies.get(id)!;
            const physics = this.#physicsState.bodies.get(id)!;

            const bodyView = {id, app, physics};
            this.#bodyViews.set(id, bodyView);
            this.#bodyCreated.push(bodyView);
        }
    }

    #refreshUpdatedBodies() {
        this.#bodyUpdated.length = 0;

        // Non-null assertion is safe because of prior validation in #validateDiffs()
        for(const id of this.#bodyUpdatedDiffUnion) {
            this.#bodyUpdated.push(this.#bodyViews.get(id)!);
        }
    }

    #refreshDeletedBodies() {
        for(const id of this.#physicsDiff.bodies.deleted) this.#bodyViews.delete(id);

        // `this.#bodyFrameData.deleted` is set directly in the constructor and directly 
        // references `this.#physicsDiff.bodies.deleted`. As long as validation passes, this
        // is always correct for the current frame.
    }
}
