import { expect, it, describe, beforeEach } from "vitest";
import AppData, { DEFAULT_SIM_DATA } from "@webapp/scripts/Data/AppData";

describe("AppData", () => {
    let appData: AppData;

    beforeEach(() => {
        appData = new AppData();
    });

    describe("constructor and initial state", () => {
        it("should initialize with an default initial state", () => {
            expect(appData.diff.sim.size).toBe(0);
            expect(appData.diff.updatedBodies.size).toBe(0);
            expect(appData.state.sim).toEqual(DEFAULT_SIM_DATA);
            expect(appData.state.bodies.size).toBe(0);
        });
    });

    describe("updateSimulationData()", () => {
        it("should immediately update state and leave other properties unchanged", () => {
            const newOrbitPaths = !DEFAULT_SIM_DATA.enableOrbitPaths;
            appData.updateSimulationData({enableOrbitPaths: newOrbitPaths});
            expect(appData.state.sim).toEqual({ 
                ...DEFAULT_SIM_DATA,
                enableOrbitPaths: newOrbitPaths
            });
        });

        it("should record changes in the diff only after syncing", () => {
            const newOrbitPaths = !DEFAULT_SIM_DATA.enableOrbitPaths;
            const newBodyLabels = !DEFAULT_SIM_DATA.enableBodyLabels;
            appData.updateSimulationData({
                enableOrbitPaths: newOrbitPaths,
                enableBodyLabels: newBodyLabels
            });
            expect(appData.diff.sim.size).toBe(0);

            appData.syncDiff(new Set(), new Set());
            expect(appData.diff.sim.size).toBe(2);
            expect(appData.diff.sim).toContain("enableOrbitPaths");
            expect(appData.diff.sim).toContain("enableBodyLabels");
        });

        it("shouldn't record a change if the new value is the same as the old value", () => {
            appData.updateSimulationData({ enableOrbitPaths: DEFAULT_SIM_DATA.enableOrbitPaths });
            appData.syncDiff(new Set(), new Set());
            expect(appData.diff.sim.size).toBe(0);
        });

        it("should handle an empty update object gracefully without flagging an update", () => {
            const initialStateValues = {...appData.state.sim};
            appData.updateSimulationData({});
            expect(appData.state.sim).toEqual(initialStateValues);

            appData.syncDiff(new Set(), new Set());
            expect(appData.diff.sim.size).toBe(0);
        });
    });

    describe("updateBodyData()", () => {
        it("should immediately update state and leave other properties unchanged", () => {
            appData.syncDiff(new Set([0]), new Set());  // Create body
            const initialBodyState = { ...appData.state.bodies.get(0)! };

            appData.updateBodyData(0, {name: "Earth"});
            expect(appData.state.bodies.size).toBe(1);
            expect(appData.state.bodies.get(0)).toEqual({
                ...initialBodyState,
                name: "Earth"
            });
        });

        it("should record id in diff only after syncing", () => {
            appData.syncDiff(new Set([0]), new Set());
            appData.updateBodyData(0, {name: "Earth"});
            expect(appData.diff.updatedBodies.size).toBe(0);

            appData.syncDiff(new Set(), new Set());
            expect(appData.diff.updatedBodies.size).toBe(1);
            expect(appData.diff.updatedBodies.has(0)).toBe(true);
        });

        it("should return false if the body doesn't exist, true otherwise", () => {
            const foundEarth = appData.updateBodyData(0, {name: "Earth"});
            expect(foundEarth).toBe(false);

            appData.syncDiff(new Set([1]), new Set());
            const foundMars = appData.updateBodyData(1, {name: "Mars"});
            expect(foundMars).toBe(true);
        });

        it("shouldn't flag a body as updated if the new values are the same as the old values", () => {
            appData.syncDiff(new Set([0]), new Set());
            const initialBodyState = { ...appData.state.bodies.get(0)! };
            appData.updateBodyData(0, {name: initialBodyState.name});
            expect(appData.state.bodies.get(0)).toEqual(initialBodyState);
            
            appData.syncDiff(new Set(), new Set());
            expect(appData.diff.updatedBodies.size).toBe(0);
        });

        it("should handle an empty update object gracefully without flagging an update", () => {
            appData.syncDiff(new Set([0]), new Set());
            const initialBodyState = { ...appData.state.bodies.get(0)! };
            
            appData.updateBodyData(0, {});
            expect(appData.state.bodies.get(0)).toEqual(initialBodyState);

            appData.syncDiff(new Set(), new Set());
            expect(appData.diff.updatedBodies.size).toBe(0);
        });
    });

    describe("syncDiff()", () => {
        it("should create new bodies with default data", () => {
            appData.syncDiff(new Set([2, 10]), new Set());
            const bodies = appData.state.bodies;
            expect(bodies.size).toBe(2);
            expect(bodies.has(2)).toBe(true);
            expect(bodies.has(10)).toBe(true);
            expect(bodies.get(2)?.name).toBe("New Body #2");
        });

        it("should delete existing bodies", () => {
            appData.syncDiff(new Set([1, 2, 3]), new Set());
            appData.syncDiff(new Set(), new Set([1, 3]));
            const bodies = appData.state.bodies;
            expect(bodies.size).toBe(1);
            expect(bodies.has(2)).toBe(true);
        });

        it("should not count created or deleted bodies in the updatedBodies diff", () => {
            appData.syncDiff(new Set([1,2,3,4]), new Set());
            expect(appData.diff.updatedBodies.size).toBe(0);

            appData.syncDiff(new Set(), new Set([2,3]));
            expect(appData.diff.updatedBodies.size).toBe(0);
        });

        it("should clear previous frame's diff", () => {
            appData.syncDiff(new Set([1]), new Set());
            appData.updateSimulationData({ enableOrbitPaths: !DEFAULT_SIM_DATA.enableOrbitPaths });
            appData.updateBodyData(1, { name: "Earth" });
            expect(appData.diff.sim.size).toBe(0);
            expect(appData.diff.updatedBodies.size).toBe(0);

            appData.syncDiff(new Set(), new Set());
            expect(appData.diff.sim.size).toBe(1);
            expect(appData.diff.sim.has("enableOrbitPaths")).toBe(true);
            expect(appData.diff.updatedBodies.size).toBe(1);
            expect(appData.diff.updatedBodies.has(1)).toBe(true);

            appData.updateSimulationData({ enableBodyLabels: !DEFAULT_SIM_DATA.enableBodyLabels });
            appData.syncDiff(new Set(), new Set());
            expect(appData.diff.sim.size).toBe(1);
            expect(appData.diff.sim.has("enableOrbitPaths")).toBe(false);
            expect(appData.diff.sim.has("enableBodyLabels")).toBe(true);
            expect(appData.diff.updatedBodies.size).toBe(0);
        });

        it("should handle deleting a non-existent body gracefully", () => {
            expect(() => appData.syncDiff(new Set(), new Set([1]))).not.toThrow();
        });

        it("should not overwrite the data of an existing body when the same body's id appears in the 'created' set", () => {
            appData.syncDiff(new Set([1]), new Set());

            const newBodyName = "Glise 187b"
            appData.updateBodyData(1, { name: newBodyName});
            appData.syncDiff(new Set(), new Set());
            expect(appData.state.bodies.size).toBe(1);
            expect(appData.diff.updatedBodies.size).toBe(1);

            appData.syncDiff(new Set([1]), new Set());
            expect(appData.state.bodies.size).toBe(1);
            expect(appData.diff.updatedBodies.size).toBe(0);
            expect(appData.state.bodies.get(1)?.name).toBe(newBodyName);
        });

        it("shouldn't record an update in the diff if the body was deleted in the same frame", () => {
            appData.syncDiff(new Set([1]), new Set());
            appData.updateBodyData(1, { name: "Earth"});
            appData.syncDiff(new Set(), new Set([1]));
            expect(appData.diff.updatedBodies.size).toBe(0);
        });
    });

    describe("getPresetData()", () => {
        it("should return a copy of the state that cannot mutate the original", () => {
            appData.syncDiff(new Set([1]), new Set());
            appData.updateBodyData(1, { name: "Jupiter" });

            const preset = appData.getPresetData();
            expect(preset.bodies?.[0]?.name).toBe("Jupiter");   // Sanity check for preset shape.

            // Mutate the retrieved preset
            preset.sim.enableBodyLabels = !preset.sim.enableBodyLabels;
            preset.bodies[0].name = "Not Jupiter";

            // Verify the original state is untouched
            expect(appData.state.sim.enableBodyLabels).not.toBe(preset.sim.enableBodyLabels);
            expect(appData.state.bodies.get(1)?.name).toBe("Jupiter");
        });
    });

    describe("loadPresetData()", () => {
        it("should populate the next diff", () => {
            // Frame 0 - Pre-Render
            appData.syncDiff(new Set([1]), new Set());

            // Frame 0 - Post-Render
            appData.updateSimulationData({ enableOrbitPaths: !DEFAULT_SIM_DATA.enableOrbitPaths });
            appData.updateBodyData(1, { name: "Earth" });
            const preset = appData.getPresetData(); 

            // Frame 1 - Pre-Render
            appData.syncDiff(new Set(), new Set());
            expect(appData.diff.sim.size).toBe(1);
            expect(appData.diff.sim.has("enableOrbitPaths")).toBe(true);
            expect(appData.diff.updatedBodies.size).toBe(1);
            expect(appData.diff.updatedBodies.has(1)).toBe(true);

            // Frame 1 - Post-Render
            appData.updateSimulationData({ enableBodyLabels: !DEFAULT_SIM_DATA.enableBodyLabels });
            appData.updateBodyData(1, { name: "Not Earth" });

            // Frame 2 - Pre-Render
            appData.syncDiff(new Set(), new Set());
            expect(appData.diff.sim.size).toBe(1);
            expect(appData.diff.sim.has("enableBodyLabels")).toBe(true);
            expect(appData.diff.updatedBodies.size).toBe(1);
            expect(appData.diff.updatedBodies.has(1)).toBe(true);
            
            // Frame 2 - Post-Render
            appData.loadPresetData(preset);

            // Frame 3 - Pre-Render
            appData.syncDiff(new Set(), new Set());
            expect(appData.diff.sim.size).toBe(1);
            expect(appData.diff.updatedBodies.size).toBe(1);
        });

        it("should delete bodies that are not in the preset", () => {
            appData.syncDiff(new Set([1]), new Set());
            expect(appData.state.bodies.size).toBe(1);
            const preset = appData.getPresetData();

            // Add more bodies
            appData.syncDiff(new Set([2, 3]), new Set());
            expect(appData.state.bodies.size).toBe(3);

            // Load the preset with 1 body
            appData.loadPresetData(preset);
            expect(appData.state.bodies.size).toBe(1);
            expect(appData.state.bodies.has(1)).toBe(true);
        });

        it("should ensure stable references to updated bodies", () => {
            // Set up a state with a body id 
            appData.syncDiff(new Set([1]), new Set());
            appData.updateBodyData(1, {name: "Earth"});
            const obj = appData.state.bodies.get(1);
            expect(obj).toBeTruthy();

            // Create a preset from an entirely different appData instance that has a body with the same id
            const otherAppData = new AppData();
            otherAppData.syncDiff(new Set([1]), new Set());
            otherAppData.updateBodyData(1, {name: "Moon"});
            const preset = otherAppData.getPresetData();

            // Load the preset
            appData.loadPresetData(preset);
            expect(appData.state.bodies.get(1)).toBe(obj);
            expect(appData.state.bodies.get(1)?.name).toBe("Moon");
        });
    })

    describe("getPresetData() & loadPresetData()", () => {
        it("should maintain state across serialization and deserialization", () => {
            // Setup and serialize initial state in one instance
            appData.syncDiff(new Set([1, 2]), new Set()); 
            appData.updateSimulationData({ enableOrbitPaths: !DEFAULT_SIM_DATA.enableOrbitPaths });
            appData.updateBodyData(1, { name: "Earth" });
            const preset = appData.getPresetData();

            // Create another instance and load the preset
            const newAppData = new AppData();
            newAppData.loadPresetData(preset);
            expect(newAppData.state.sim).toEqual(appData.state.sim);
            expect(newAppData.state.bodies).toEqual(appData.state.bodies);
        });
    });
});