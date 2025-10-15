import { BodyId, PhysicsStateBody, PhysicsStateSim, PhysicsState, PhysicsDiff } from "@bridge";
import { AppStateBody, AppStateSim, AppState, AppDiff } from "./AppData";

export interface BodyView {
    readonly id: BodyId,
    readonly app: Readonly<AppStateBody>;
    readonly physics: Readonly<PhysicsStateBody>;
}

export interface SimView {
    readonly app: Readonly<AppStateSim>;
    readonly physics: Readonly<PhysicsStateSim>;
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
     * it can use {@link DataViews.appDiff.bodies.updated} or 
     * {@link DataViews.physicsDiff.bodies.updated} respectively.
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

export class DataValidationError extends Error {
    constructor(message: string) {
        super(message);
        this.name = "DataValidationError";
    }
}

/** Data view provider for UI components. */
export interface IDataViews {
    /** The SimView object holding references to live data. */
    simView: SimView;
    /** A map of BodyIds to BodyView objects holding references to live data. */
    bodyViews: Map<BodyId, BodyView>;
    /** Transient, prepared data of simulation property keys that were updated this frame. */
    simFrameData: SimFrameData;
    /** Transient, prepared data of bodies that were created, updated, or deleted this frame. */
    bodyFrameData: BodyFrameData;
    /** Contains the direct diff information about physics data changes during the last engine tick. */
    physicsDiff: PhysicsDiff;
    /** Contains the direct diff information about app data changes during the last engine tick. */
    appDiff: AppDiff;
    /**
     * Processes and refreshes the data views and frameData by reading from the injected sources.
     * @param physicsState  Reference to the physics state object.
     * @param physicsDiff   Reference to the physics diff object.
     * @param appState      Reference to the appData state object.
     * @param appDiff       Reference to the appData diff object.
     * @throws {DataValidationError} If any issue or inconsistency with the passed data is detected.
     */
    refresh(
        physicsState: PhysicsState,
        physicsDiff: PhysicsDiff,
        appState: AppState,
        appDiff: AppDiff
    ): void;
}

export default class DataViews implements IDataViews {
    // Data sources are not guaranteed to be available during instantiation, 
    // so they're set on first refresh instead.
    #physicsState!: PhysicsState;
    #physicsDiff!: PhysicsDiff;
    #appState!: AppState;
    #appDiff!: AppDiff;

    #simView: SimView;
    #bodyViews: Map<BodyId, BodyView> = new Map();

    #simFrameData: SimFrameData;
    #bodyFrameData: BodyFrameData;

    #bodyCreated: BodyView[] = [];
    #bodyUpdated: BodyView[] = [];

    #bodyUpdatedDiffUnion: Set<BodyId> = new Set();

    constructor() {
        const instance = this;
        this.#simView = {
            get app() { return instance.#appState.sim },
            get physics() { return instance.#physicsState.sim }
        };

        this.#simFrameData = {
            get app() { return instance.#appDiff.sim },
            get physics() { return instance.#physicsDiff.sim }
        };

        this.#bodyFrameData = {
            get created() { return instance.#bodyCreated },
            get updated() { return instance.#bodyUpdated },
            get deleted() { return instance.#physicsDiff.bodies.deleted },
        }
    }

    get simView(): SimView { return this.#simView; }
    get bodyViews(): Map<BodyId, BodyView> { return this.#bodyViews; }
    get simFrameData(): SimFrameData { return this.#simFrameData; }
    get bodyFrameData(): BodyFrameData { return this.#bodyFrameData; }
    get physicsDiff(): PhysicsDiff { return this.#physicsDiff; }
    get appDiff(): AppDiff { return this.#appDiff; }

    refresh(
        physicsState: PhysicsState,
        physicsDiff: PhysicsDiff,
        appState: AppState,
        appDiff: AppDiff
    ): void {
        // Set state data references if they don't already exist.
        if(!this.#physicsState) this.#physicsState = physicsState;
        if(!this.#appState) this.#appState = appState;

        if(__DEBUG__) {
            // State data is stored by reference in BodyView objects and changing it would invalidate those references.
            if(this.#physicsState !== physicsState) throw new DataValidationError("Invalid reference to underlying data source (physicsState), which must remain unchanged after the first call.");
            if(this.#appState !== appState) throw new DataValidationError("Invalid reference to underlying data source (appState), which must remain unchanged after the first call.");
        }

        // Diff data isn't stored by reference anywhere so it can be reassigned safely to allow for double buffered implementations. 
        this.#physicsDiff = physicsDiff;
        this.#appDiff = appDiff;
        
        // Create a union of updated body diffs.
        // Set.prototype.union() is faster but creates a new Set every frame.
        this.#bodyUpdatedDiffUnion.clear();
        for(const id of physicsDiff.bodies.updated) this.#bodyUpdatedDiffUnion.add(id);
        for(const id of appDiff.updatedBodies) this.#bodyUpdatedDiffUnion.add(id);

        // Validate before refresh methods as those rely on valid data!
        if(__DEBUG__) this.#validateDiffs(appDiff, physicsDiff);

        // After validation it doesn't matter in which order these are run.
        this.#refreshCreatedBodies(physicsDiff.bodies.created);
        this.#refreshUpdatedBodies(this.#bodyUpdatedDiffUnion);
        this.#refreshDeletedBodies(physicsDiff.bodies.deleted);
    }

    #validateDiffs(appDiff: AppDiff, physicsDiff: PhysicsDiff) {
        const app = appDiff.updatedBodies;
        const physics = physicsDiff.bodies;

        // Note: Be careful to not break the sequential cohesion of these checks when refactoring!

        // Validate - Created
        if(!physics.created.isSubsetOf(this.#physicsState.bodies)) throw new DataValidationError("Invalid diff: Not all created bodies have a PhysicsState.");

        // Validate - Deleted
        if(!physics.deleted.isDisjointFrom(physics.created)) throw new DataValidationError("Invalid diff: Overlap between created and deleted body diffs detected.");
        if(!physics.deleted.isSubsetOf(this.#bodyViews)) throw new DataValidationError("Invalid diff: Not all deleted bodies have a DataView.");  

        // Validate - Updated (individual)
        if(!app.isDisjointFrom(physics.created)) throw new DataValidationError("Invalid diff: Overlap between created and updated(app) body diffs detected.");
        if(!app.isDisjointFrom(physics.deleted)) throw new DataValidationError("Invalid diff: Overlap between deleted and updated(app) body diffs detected.");
        if(!physics.updated.isDisjointFrom(physics.created)) throw new DataValidationError("Invalid diff: Overlap between created and updated(physics) body diffs detected.");
        if(!physics.updated.isDisjointFrom(physics.deleted)) throw new DataValidationError("Invalid diff: Overlap between deleted and updated(physics) body diffs detected.");

        // Validate - Updated (union)
        if(!this.#bodyUpdatedDiffUnion.isSubsetOf(this.#bodyViews)) throw new DataValidationError("Invalid diff: Not all updated bodies have a DataView.");
    }

    #refreshCreatedBodies(created: ReadonlySet<BodyId>) {
        this.#bodyCreated.length = 0;

        for(const id of created) {
            // Non-null assertions are safe because of prior validation in #validateDiffs()
            const app = this.#appState.bodies.get(id)!;
            const physics = this.#physicsState.bodies.get(id)!;

            const bodyView = {id, app, physics};
            this.#bodyViews.set(id, bodyView);
            this.#bodyCreated.push(bodyView);
        }
    }

    #refreshUpdatedBodies(updated: ReadonlySet<BodyId>) {
        this.#bodyUpdated.length = 0;

        // Non-null assertion is safe because of prior validation in #validateDiffs()
        for(const id of updated) {
            this.#bodyUpdated.push(this.#bodyViews.get(id)!);
        }
    }

    #refreshDeletedBodies(deleted: ReadonlySet<BodyId>) {
        for(const id of deleted) this.#bodyViews.delete(id);
    }
}
