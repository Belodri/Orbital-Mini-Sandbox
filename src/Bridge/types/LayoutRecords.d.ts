// AUTO-GENERATED FILE FROM C# SSOT. DO NOT EDIT.
// Re-run the TsTypeGen tool to regenerate.

export interface SimStateLayout {
    /**
     * Internal simulation time.
     */
    readonly simulationTime: number;
    /**
     * A multiplier for how much the simulationTime advances during each simulation tick. Default = 1; Min = 0.01; Max = 100;
     */
    readonly timeScale: number;
    /**
     * Determines direction of simulationTime. Forward = 1; Backward = 0;
     */
    readonly timeIsForward: number;
    /**
     * Determines how many ms in Simulation time is 1ms in real time, assuming TimeScale = 1.
     */
    readonly timeConversionFactor: number;
    /**
     * The total number of bodies in the simulation, including disabled ones.
     */
    readonly bodyCount: number;
    /**
     * The opening-angle parameter (theta, θ) for the Barnes-Hut algorithm. 0
     */
    readonly theta: number;
    /**
     * The value for the gravitational constant G in m3/kg/s^2
     */
    readonly gravitationalConstant: number;
    /**
     * The softening factor (epsilon, ε) used to prevent numerical instability in the simulation. 0
     */
    readonly epsilon: number;
}

export interface BodyStateLayout {
    /**
     * The unique identifier of the body
     */
    readonly id: number;
    /**
     * Disabled bodies are ignored by the simulation. Enabled = 1; Disabled = 0;
     */
    readonly enabled: number;
    /**
     * The mass of the body
     */
    readonly mass: number;
    /**
     * The x position of the body
     */
    readonly posX: number;
    /**
     * The y position of the body
     */
    readonly posY: number;
    /**
     * The body's velocity in the x direction
     */
    readonly velX: number;
    /**
     * The body's velocity in the y direction
     */
    readonly velY: number;
    /**
     * The body's acceleration in the x direction
     */
    readonly accX: number;
    /**
     * The body's acceleration in the y direction
     */
    readonly accY: number;
}