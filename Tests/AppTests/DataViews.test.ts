import { expect, it, describe, beforeEach, beforeAll, afterAll, vi } from "vitest";
import DataViews, { DataValidationError } from "@webapp/scripts/Data/DataViews";
import { mockAppDiff, mockAppState, mockAppStateBody, mockPhysicsDiff, mockPhysicsState, mockPhysicsStateBody } from "./mockTypes";
import { 
    AppStateBody, AppStateSim, PhysicsStateBody, PhysicsStateSim, PhysicsState, PhysicsDiff, AppState, AppDiff,
    BodyId, BodyView, SimView, BodyFrameData, SimFrameData,
} from "./MutableTypes"

const addBody = (
    id: BodyId,
    physicsState: PhysicsState,
    appState: AppState,
    physicsProps: Partial<PhysicsStateBody> = {},
    appProps: Partial<AppStateBody> = {}
) => {
    const physicsBody: PhysicsStateBody = mockPhysicsStateBody({id, ...physicsProps});
    const appBody: AppStateBody = mockAppStateBody({name: `Body ${id}`, ...appProps});
    physicsState.bodies.set(id, physicsBody);
    appState.bodies.set(id, appBody);
};

describe('DataViews', () => {
    let dataViews: DataViews;
    let physicsState: PhysicsState;
    let appState: AppState;
    let physicsDiff: PhysicsDiff;
    let appDiff: AppDiff;

    beforeEach(() => {
        dataViews = new DataViews();

        physicsState = mockPhysicsState();
        appState = mockAppState();
        physicsDiff = mockPhysicsDiff();
        appDiff = mockAppDiff();
    });

    describe('constructor and initial state', () => {
        it('should initialize with an empty initial state', () => {
            expect(dataViews).toBeInstanceOf(DataViews);
            expect(dataViews.bodyViews.size).toBe(0);
            expect(dataViews.bodyFrameData.created).toEqual([]);
            expect(dataViews.bodyFrameData.updated).toEqual([]);
            expect(dataViews.physicsDiff).toBeUndefined()
            expect(dataViews.appDiff).toBeUndefined()
        });

        it('should throw when accessing data sources before the first refresh', () => {
            expect(() => dataViews.simView.app).toThrow();
            expect(() => dataViews.simView.physics).toThrow();
            expect(() => dataViews.simFrameData.app).toThrow();
            expect(() => dataViews.simFrameData.physics).toThrow();
            expect(() => dataViews.bodyFrameData.deleted).toThrow();
        });
    });

    describe('refresh method', () => {
        it('should set internal state and diff references on the first call', () => {
            dataViews.refresh(physicsState, physicsDiff, appState, appDiff);

            expect(dataViews.simView.app).toBe(appState.sim);
            expect(dataViews.simView.physics).toBe(physicsState.sim);
            expect(dataViews.physicsDiff).toBe(physicsDiff);
            expect(dataViews.appDiff).toBe(appDiff);
        });

        it('should update diff references on subsequent calls', () => {
            dataViews.refresh(physicsState, physicsDiff, appState, appDiff);
            expect(dataViews.physicsDiff).toBe(physicsDiff);
            expect(dataViews.appDiff).toBe(appDiff);

            const physicsDiff2 = mockPhysicsDiff();
            const appDiff2 = mockAppDiff();

            dataViews.refresh(physicsState, physicsDiff2, appState, appDiff2);
            expect(dataViews.physicsDiff).toBe(physicsDiff2);
            expect(dataViews.appDiff).toBe(appDiff2);
            expect(dataViews.physicsDiff).not.toBe(physicsDiff);
            expect(dataViews.appDiff).not.toBe(appDiff);
        });
    });

    describe('Body Lifecycle Management', () => {
        it('should handle body creation', () => {
            addBody(1, physicsState, appState);
            physicsDiff.bodies.created.add(1);

            dataViews.refresh(physicsState, physicsDiff, appState, appDiff);

            expect(dataViews.bodyViews.size).toBe(1);
            expect(dataViews.bodyViews.has(1)).toBe(true);

            const bodyView = dataViews.bodyViews.get(1)!;
            expect(bodyView.id).toBe(1);
            expect(bodyView.app).toBe(appState.bodies.get(1));
            expect(bodyView.physics).toBe(physicsState.bodies.get(1));

            expect(dataViews.bodyFrameData.created).toHaveLength(1);
            expect(dataViews.bodyFrameData.created[0]).toBe(bodyView);
            expect(dataViews.bodyFrameData.updated).toHaveLength(0);
            expect(dataViews.bodyFrameData.deleted.size).toBe(0);
        });

        it('should handle body updates from physics diff', () => {
            // Frame 1: Create body
            addBody(1, physicsState, appState);
            physicsDiff.bodies.created.add(1);
            dataViews.refresh(physicsState, physicsDiff, appState, appDiff);

            // Frame 2: Update body
            const physicsDiff2 = mockPhysicsDiff();
            physicsDiff2.bodies.updated.add(1);
            const appDiff2 = mockAppDiff();
            dataViews.refresh(physicsState, physicsDiff2, appState, appDiff2);

            expect(dataViews.bodyViews.size).toBe(1);
            expect(dataViews.bodyFrameData.created).toHaveLength(0);
            expect(dataViews.bodyFrameData.updated).toHaveLength(1);
            expect(dataViews.bodyFrameData.updated[0].id).toBe(1);
            expect(dataViews.bodyFrameData.deleted.size).toBe(0);
        });

        it('should handle body updates from app diff', () => {
            // Frame 1: Create body
            addBody(1, physicsState, appState);
            physicsDiff.bodies.created.add(1);
            dataViews.refresh(physicsState, physicsDiff, appState, appDiff);

            // Frame 2: Update body
            const physicsDiff2 = mockPhysicsDiff();
            const appDiff2 = mockAppDiff();
            appDiff2.updatedBodies.add(1);
            dataViews.refresh(physicsState, physicsDiff2, appState, appDiff2);

            expect(dataViews.bodyViews.size).toBe(1);
            expect(dataViews.bodyFrameData.created).toHaveLength(0);
            expect(dataViews.bodyFrameData.updated).toHaveLength(1);
            expect(dataViews.bodyFrameData.updated[0].id).toBe(1);
            expect(dataViews.bodyFrameData.deleted.size).toBe(0);
        });

        it('should handle body updates from both diffs without duplication', () => {
            // Frame 1: Create body
            addBody(1, physicsState, appState);
            physicsDiff.bodies.created.add(1);
            dataViews.refresh(physicsState, physicsDiff, appState, appDiff);

            // Frame 2: Update body in both diffs
            const physicsDiff2 = mockPhysicsDiff();
            physicsDiff2.bodies.updated.add(1);
            const appDiff2 = mockAppDiff();
            appDiff2.updatedBodies.add(1);
            dataViews.refresh(physicsState, physicsDiff2, appState, appDiff2);

            expect(dataViews.bodyFrameData.updated).toHaveLength(1);
            expect(dataViews.bodyFrameData.updated[0].id).toBe(1);
        });

        it('should handle body deletion', () => {
            // Frame 1: Create body
            addBody(1, physicsState, appState);
            physicsDiff.bodies.created.add(1);
            dataViews.refresh(physicsState, physicsDiff, appState, appDiff);
            expect(dataViews.bodyViews.has(1)).toBe(true);

            // Frame 2: Delete body
            const physicsDiff2 = mockPhysicsDiff();
            physicsDiff2.bodies.deleted.add(1);
            const appDiff2 = mockAppDiff();
            dataViews.refresh(physicsState, physicsDiff2, appState, appDiff2);

            expect(dataViews.bodyViews.size).toBe(0);
            expect(dataViews.bodyViews.has(1)).toBe(false);
            expect(dataViews.bodyFrameData.created).toHaveLength(0);
            expect(dataViews.bodyFrameData.updated).toHaveLength(0);
            expect(dataViews.bodyFrameData.deleted.size).toBe(1);
            expect(dataViews.bodyFrameData.deleted.has(1)).toBe(true);
        });

        it('should handle combined create, update, and delete in one frame', () => {
            // Initial state: bodies 2 and 3 exist
            addBody(2, physicsState, appState);
            addBody(3, physicsState, appState);
            const initialPhysicsDiff = mockPhysicsDiff();
            initialPhysicsDiff.bodies.created.add(2);
            initialPhysicsDiff.bodies.created.add(3);
            dataViews.refresh(physicsState, initialPhysicsDiff, appState, mockAppDiff());

            // Frame with C/U/D operations
            // Create body 1
            addBody(1, physicsState, appState);
            physicsDiff.bodies.created.add(1);

            // Update body 2
            physicsDiff.bodies.updated.add(2);
            appDiff.updatedBodies.add(2); // In both for good measure

            // Delete body 3
            physicsDiff.bodies.deleted.add(3);

            dataViews.refresh(physicsState, physicsDiff, appState, appDiff);

            // Assert final state
            expect(dataViews.bodyViews.size).toBe(2);
            expect(dataViews.bodyViews.has(1)).toBe(true);
            expect(dataViews.bodyViews.has(2)).toBe(true);
            expect(dataViews.bodyViews.has(3)).toBe(false);

            // Assert frame data
            expect(dataViews.bodyFrameData.created).toHaveLength(1);
            expect(dataViews.bodyFrameData.created[0].id).toBe(1);

            expect(dataViews.bodyFrameData.updated).toHaveLength(1);
            expect(dataViews.bodyFrameData.updated[0].id).toBe(2);

            expect(dataViews.bodyFrameData.deleted.size).toBe(1);
            expect(dataViews.bodyFrameData.deleted.has(3)).toBe(true);
        });
    });

    describe('Frame Data Management', () => {
        it('should clear and repopulate bodyFrameData on each refresh', () => {
            // Frame 1: Create body 1
            addBody(1, physicsState, appState);
            physicsDiff.bodies.created.add(1);
            dataViews.refresh(physicsState, physicsDiff, appState, appDiff);

            expect(dataViews.bodyFrameData.created).toHaveLength(1);
            expect(dataViews.bodyFrameData.updated).toHaveLength(0);
            expect(dataViews.bodyFrameData.deleted.size).toBe(0);

            // Frame 2: No changes
            const physicsDiff2 = mockPhysicsDiff();
            const appDiff2 = mockAppDiff();
            dataViews.refresh(physicsState, physicsDiff2, appState, appDiff2);

            expect(dataViews.bodyViews.size).toBe(1); // Body view persists
            expect(dataViews.bodyFrameData.created).toHaveLength(0); // Frame data is cleared
            expect(dataViews.bodyFrameData.updated).toHaveLength(0);
            expect(dataViews.bodyFrameData.deleted.size).toBe(0);
        });

        it('simFrameData should correctly proxy the diff data', () => {
            physicsDiff.sim.add("theta");
            appDiff.sim.add("enableOrbitPaths");
            
            dataViews.refresh(physicsState, physicsDiff, appState, appDiff);

            expect(dataViews.simFrameData.physics).toEqual(new Set(["theta"]));
            expect(dataViews.simFrameData.app).toEqual(new Set(["enableOrbitPaths"]));

            const physicsDiff2 = mockPhysicsDiff();
            physicsDiff2.sim.add("timeStep");
            const appDiff2 = mockAppDiff();
            appDiff2.sim.add("enableBodyLabels");

            dataViews.refresh(physicsState, physicsDiff2, appState, appDiff2);

            expect(dataViews.simFrameData.physics).toEqual(new Set(["timeStep"]));
            expect(dataViews.simFrameData.app).toEqual(new Set(["enableBodyLabels"]));
        });
    });

    describe('Validation with __DEBUG__ enabled', () => {
        beforeAll(() => vi.stubGlobal('__DEBUG__', true));
        afterAll(() => vi.unstubAllGlobals());

        it('should throw DataValidationError if physicsState reference changes on subsequent calls', () => {
            dataViews.refresh(physicsState, physicsDiff, appState, appDiff);
            const newPhysicsState = mockPhysicsState();
            expect(() => dataViews.refresh(newPhysicsState, physicsDiff, appState, appDiff)).toThrow(DataValidationError);
        });

        it('should throw DataValidationError if appState reference changes on subsequent calls', () => {
            dataViews.refresh(physicsState, physicsDiff, appState, appDiff);
            const newAppState = mockAppState();
            expect(() => dataViews.refresh(physicsState, physicsDiff, newAppState, appDiff)).toThrow(DataValidationError);
        });

        it('should not throw if state references remain the same on subsequent calls', () => {
            dataViews.refresh(physicsState, physicsDiff, appState, appDiff);
            expect(() => dataViews.refresh(physicsState, physicsDiff, appState, appDiff)).not.toThrow();
        });

        it('should throw if a created body is not in physicsState', () => {
            physicsDiff.bodies.created.add(1); // Body 1 does not exist in physicsState
            expect(() => dataViews.refresh(physicsState, physicsDiff, appState, appDiff)).toThrowError(DataValidationError);
        });

        it('should throw for overlap between created and deleted body diffs', () => {
            addBody(1, physicsState, appState);
            physicsDiff.bodies.created.add(1);
            physicsDiff.bodies.deleted.add(1);
            expect(() => dataViews.refresh(physicsState, physicsDiff, appState, appDiff)).toThrowError(DataValidationError);
        });

        it('should throw if a deleted body does not have a DataView', () => {
            physicsDiff.bodies.deleted.add(1); // Body 1 was never created, so no DataView exists for it
            expect(() => dataViews.refresh(physicsState, physicsDiff, appState, appDiff)).toThrowError(DataValidationError);
        });
        
        it('should throw for overlap between created and updated(app) body diffs', () => {
            addBody(1, physicsState, appState);
            physicsDiff.bodies.created.add(1);
            appDiff.updatedBodies.add(1);
            expect(() => dataViews.refresh(physicsState, physicsDiff, appState, appDiff)).toThrowError(DataValidationError);
        });

        it('should throw for overlap between created and updated(physics) body diffs', () => {
            addBody(1, physicsState, appState);
            physicsDiff.bodies.created.add(1);
            physicsDiff.bodies.updated.add(1);
            expect(() => dataViews.refresh(physicsState, physicsDiff, appState, appDiff)).toThrowError(DataValidationError);
        });
        
        it('should throw for overlap between deleted and updated(app) body diffs', () => {
            addBody(1, physicsState, appState);
            // Frame 1: Create the body so a DataView exists.
            const pDiff1 = mockPhysicsDiff();
            pDiff1.bodies.created.add(1);
            dataViews.refresh(physicsState, pDiff1, appState, mockAppDiff());
            
            // Frame 2: Attempt to delete and update the same body.
            const pDiff2 = mockPhysicsDiff();
            const aDiff2 = mockAppDiff();
            pDiff2.bodies.deleted.add(1);
            aDiff2.updatedBodies.add(1);
            expect(() => dataViews.refresh(physicsState, pDiff2, appState, aDiff2)).toThrowError(DataValidationError);
        });
        
        it('should throw for overlap between deleted and updated(physics) body diffs', () => {
            addBody(1, physicsState, appState);
            // Frame 1: Create the body so a DataView exists.
            const pDiff1 = mockPhysicsDiff();
            pDiff1.bodies.created.add(1);
            dataViews.refresh(physicsState, pDiff1, appState, mockAppDiff());

            // Frame 2: Attempt to delete and update the same body.
            const pDiff2 = mockPhysicsDiff();
            pDiff2.bodies.deleted.add(1);
            pDiff2.bodies.updated.add(1);
            expect(() => dataViews.refresh(physicsState, pDiff2, appState, mockAppDiff())).toThrowError(DataValidationError);
        });

        it('should throw if an updated body does not have a DataView', () => {
            physicsDiff.bodies.updated.add(1); // Body 1 was never created, so no DataView
            expect(() => dataViews.refresh(physicsState, physicsDiff, appState, appDiff)).toThrowError(DataValidationError);
        });

        it('should not throw for valid diffs', () => {
            // Setup a complex but valid frame
            addBody(2, physicsState, appState);
            addBody(3, physicsState, appState);
            const initialPhysicsDiff = mockPhysicsDiff();
            initialPhysicsDiff.bodies.created.add(2);
            initialPhysicsDiff.bodies.created.add(3);
            dataViews.refresh(physicsState, initialPhysicsDiff, appState, mockAppDiff());

            addBody(1, physicsState, appState);
            physicsDiff.bodies.created.add(1);
            physicsDiff.bodies.updated.add(2);
            physicsDiff.bodies.deleted.add(3);

            expect(() => dataViews.refresh(physicsState, physicsDiff, appState, appDiff)).not.toThrow();
        });
    });
});