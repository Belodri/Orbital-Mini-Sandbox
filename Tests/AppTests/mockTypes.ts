import { BodyView, SimView, BodyFrameData, SimFrameData } from "@webapp/scripts/Data/DataViews";
import { AppStateBody, AppStateSim, AppState, AppDiff } from "@webapp/scripts/Data/AppData";
import { BodyId, PhysicsStateBody, PhysicsStateSim, PhysicsState, PhysicsDiff } from "@bridge";
import { ColorSource } from "pixi.js";

export function mockAppStateBody(partial: Partial<AppStateBody> = {}): AppStateBody {
    return {
        id: 0,
        name: "",
        tint: "",
        ...partial
    }
};

export function mockAppStateSim(partial: Partial<AppStateSim> = {}): AppStateSim {
    return {
        bgColor: "#000000",
        enableOrbitPaths: false,
        enableVelocityTrais: false,
        enableBodyLabels: false,
        ...partial
    }
};

export function mockPhysicsStateBody(partial: Partial<PhysicsStateBody> = {}): PhysicsStateBody {
    return {
        id: 0,
        enabled: true,
        mass: 0,
        posX: 0,
        posY: 0,
        velX: 0,
        velY: 0,
        accX: 0,
        accY: 0,
        outOfBounds: false,
        ...partial
    }
}

export function mockPhysicsStateSim(partial: Partial<PhysicsStateSim> = {}): PhysicsStateSim {
    return {
        simulationTime: 0,
        timeStep: 1,
        bodyCount: 0,
        theta: 0.5,
        gravitationalConstant: 6.67430e-11,
        epsilon: 0.1,
        ...partial
    }
};

export function mockPhysicsState(partial: Partial<PhysicsState> = {}): PhysicsState {
    return {
        sim: mockPhysicsStateSim(),
        bodies: new Map<BodyId, PhysicsStateBody>(),
        ...partial
    }
};

export function mockPhysicsDiff(partial: Partial<PhysicsDiff> = {}): PhysicsDiff {
    return {
        sim: new Set<keyof PhysicsStateSim>(),
        bodies: {
            created: new Set<BodyId>(),
            deleted: new Set<BodyId>(),
            updated: new Set<BodyId>(),
        },
        ...partial
    }
};

export function mockAppState(partial: Partial<AppState> = {}): AppState {
    return {
        sim: mockAppStateSim(),
        bodies: new Map<BodyId, AppStateBody>(),
        ...partial
    }
};

export function mockAppDiff(partial: Partial<AppDiff> = {}): AppDiff {
    return {
        sim: new Set<keyof AppStateSim>(),
        updatedBodies: new Set<BodyId>(),
        ...partial
    }
};

export function mockBodyView(partial: Partial<BodyView> = {}): BodyView {
    return {
        id: 0,
        app: mockAppStateBody({ id: 0 }),
        physics: mockPhysicsStateBody({ id: 0 }),
        ...partial
    }
};

export function mockSimView(partial: Partial<SimView> = {}): SimView {
    return {
        app: mockAppStateSim(),
        physics: mockPhysicsStateSim(),
        ...partial
    }
};

export function mockBodyFrameData(partial: Partial<BodyFrameData> = {}): BodyFrameData {
    return {
        created: [],
        updated: [],
        deleted: new Set<BodyId>(),
        ...partial
    }
};

export function mockSimFrameData(partial: Partial<SimFrameData> = {}): SimFrameData {
    return {
        app: new Set<keyof AppStateSim>(),
        physics: new Set<keyof PhysicsStateSim>(),
        ...partial
    }
};