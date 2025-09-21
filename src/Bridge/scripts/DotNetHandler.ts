import type { DotnetHostBuilder, RuntimeAPI } from "@dotnet.d.ts";
import type { BodyStateLayout, SimStateLayout } from "@LayoutRecords.d.ts";

import { dotnet as _dotnet } from 'dotnet-runtime'; // Generated during full application build
const dotnet: DotnetHostBuilder = _dotnet;

// TODO: Expand the TsTypeGen tool to generate this interface from C# source code as well.
export interface EngineBridgeAPI {     
    GetPointerData(): [simBufferPtr: number, simBufferSizeInBytes: number, bodyBufferPtr: number, bodyBufferSizeInBytes: number],
    GetSimStateLayout(): [keyof SimStateLayout],
    GetBodyStateLayout(): [keyof BodyStateLayout],
    GetSimStateTypes(): ["number"|"boolean"][],
    GetBodyStateTypes(): ["number"|"boolean"][],  // TODO: Implement these into StateManager for proper type castings
    Tick(syncOnly: boolean): void,
    CreateBody(): Promise<number>,
    DeleteBody(id: number): Promise<boolean>,
    UpdateBody(id: number, enabled: boolean | null, mass: number | null, posX: number | null, posY: number | null, velX: number | null, velY: number | null): Promise<boolean>,
    UpdateSimulation(timeStep: number | null, theta: number | null, g_SI: number | null, epsilon: number | null): Promise<void>,
    GetPreset(): string,
    LoadPreset(jsonPreset: string): void,
    GetLogs(number?: number): string[],
    ClearLogs(): void
}

export class DotNetHandler {
    static readonly #C_SHARP_NAMESPACE = "Bridge";
    static readonly #C_SHARP_CLASS_NAME = "EngineBridge";

    static #runtimeAPI: RuntimeAPI;
    static #engineBridgeAPI: EngineBridgeAPI;

    static #makeBuilder(dev: boolean) {
        return dotnet
            .withApplicationEnvironment(dev ? "Development" : "Production")
            .withDiagnosticTracing(dev)
            .withDebugging(dev ? 1 : 0);
    }

    static async init(mode: "PRODUCTION" | "DEVELOPMENT") {
        if(!this.#runtimeAPI) {
            const builder = this.#makeBuilder(mode === "DEVELOPMENT");
            const runtimeAPI = await builder.create();
            this.#runtimeAPI = runtimeAPI;
        }
        
        if(!this.#engineBridgeAPI) {
            const monoConfig = this.#runtimeAPI.getConfig();
            if(!monoConfig.mainAssemblyName) throw new Error("Main assembly name not defined.");

            const assemblyExports = await this.#runtimeAPI.getAssemblyExports(monoConfig.mainAssemblyName);
            this.#engineBridgeAPI = assemblyExports[this.#C_SHARP_NAMESPACE][this.#C_SHARP_CLASS_NAME] as EngineBridgeAPI;
        }

        return {
            runtime: this.#runtimeAPI,
            engineBridge: this.#engineBridgeAPI
        }
    }
}