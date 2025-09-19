import type { RuntimeAPI } from '../types/dotnet';
import type { BodyStateLayout, SimStateLayout } from '../types/LayoutRecords';
export interface EngineBridgeAPI {
    GetPointerData(): [simBufferPtr: number, simBufferSizeInBytes: number, bodyBufferPtr: number, bodyBufferSizeInBytes: number];
    GetSimStateLayout(): [keyof SimStateLayout];
    GetBodyStateLayout(): [keyof BodyStateLayout];
    Tick(instantTick: boolean): void;
    CreateBody(): Promise<number>;
    DeleteBody(id: number): Promise<boolean>;
    UpdateBody(id: number, enabled: boolean | null, mass: number | null, posX: number | null, posY: number | null, velX: number | null, velY: number | null): Promise<boolean>;
    UpdateSimulation(timeStep: number | null, theta: number | null, g_SI: number | null, epsilon: number | null): Promise<void>;
    GetPreset(): string;
    LoadPreset(jsonPreset: string): void;
    GetLogs(number?: number): string[];
    ClearLogs(): void;
}
export declare class DotNetHandler {
    #private;
    static init(mode: "PRODUCTION" | "DEVELOPMENT"): Promise<{
        runtime: RuntimeAPI;
        engineBridge: EngineBridgeAPI;
    }>;
}
//# sourceMappingURL=DotNetHandler.d.ts.map