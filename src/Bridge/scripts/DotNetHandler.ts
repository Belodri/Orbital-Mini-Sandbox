import type { DotnetHostBuilder, RuntimeAPI } from "@dotnet.d.ts";
import type { BodyStateLayout, SimStateLayout } from "@LayoutRecords.d.ts";

import { dotnet as _dotnet } from 'dotnet-runtime'; // Generated during full application build
const dotnet: DotnetHostBuilder = _dotnet;

// TODO: Expand the TsTypeGen tool to generate this interface from C# source code as well.
export interface EngineBridgeAPI {     
    GetPointerData(): [simBufferPtr: number, simBufferSizeInBytes: number, bodyBufferPtr: number, bodyBufferSizeInBytes: number],
    GetSimStateLayout(): [keyof SimStateLayout],
    GetBodyStateLayout(): [keyof BodyStateLayout],
    GetSimStateCsTypes(): ("Boolean" | "Double" | "Int32")[],
    GetBodyStateCsTypes(): ("Boolean" | "Double" | "Int32")[],
    Tick(syncOnly: boolean): void,
    CreateBody(): number,
    DeleteBody(id: number): boolean,
    UpdateBody(id: number, enabled: boolean | null, mass: number | null, posX: number | null, posY: number | null, velX: number | null, velY: number | null): boolean,
    UpdateSimulation(timeStep: number | null, theta: number | null, g_SI: number | null, epsilon: number | null): void,
    GetPreset(): string,
    LoadPreset(jsonPreset: string): void,
    GetLogs(number?: number): string[],
    ClearLogs(): void
}

const C_SHARP_CONFIG = {
    NAMESPACE: "Bridge",
    CLASS_NAME: "EngineBridge"
} as const;

export class DotNetHandler {
    static #runtime: RuntimeAPI | null = null;
    static #engineBridge: EngineBridgeAPI | null = null;

    static get runtime(): RuntimeAPI {
        if(!DotNetHandler.#runtime) throw new Error("Cannot access runtime API before initialization or after exit.");
        else return DotNetHandler.#runtime;
    }

    static get engineBridge(): EngineBridgeAPI {
        if(!DotNetHandler.#engineBridge) throw new Error("Cannot access engineBridge API before initialization or after exit.");
        else return DotNetHandler.#engineBridge;
    }
    
    /** Initializes the DotNetHandler and the underlying .NET runtime. Idempotent. */
    static async init(): Promise<void> {
        DotNetHandler.#runtime ??= await DotNetHandler.#createRuntimeAPI();
        DotNetHandler.#engineBridge ??= await DotNetHandler.#createEngineBridgeAPI();
    }

    /** Exits the DotNetHandler and the underlying .NET runtime. Idempotent. */
    static exit() {
        if(DotNetHandler.#runtime) DotNetHandler.#runtime.exit(0, "Exit DotNetHandler.");

        DotNetHandler.#runtime = null;
        DotNetHandler.#engineBridge = null;
    }

    static async #createRuntimeAPI(): Promise<RuntimeAPI> {
        const builder = dotnet
            .withApplicationEnvironment(__DEBUG__ ? "Development" : "Production")
            .withDiagnosticTracing(__DEBUG__)
            .withDebugging(__DEBUG__ ? 1 : 0);
        
        return builder.create()
    }

    static async #createEngineBridgeAPI(): Promise<EngineBridgeAPI> {
        const monoConfig = DotNetHandler.runtime.getConfig();
        if(!monoConfig.mainAssemblyName) throw new Error("Main assembly name not defined.");

        const assemblyExports = await DotNetHandler.runtime.getAssemblyExports(monoConfig.mainAssemblyName);
        const engineBridge = assemblyExports[C_SHARP_CONFIG.NAMESPACE][C_SHARP_CONFIG.CLASS_NAME] as EngineBridgeAPI;

        return engineBridge;
    }
}