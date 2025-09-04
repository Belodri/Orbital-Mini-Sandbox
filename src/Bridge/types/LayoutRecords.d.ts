// AUTO-GENERATED FILE FROM C# SSOT. DO NOT EDIT.
// Re-run the TsTypeGen tool to regenerate.

export interface SimStateLayout {
    /**
     * Internal simulation time in units of days (d).
     */
    readonly simulationTime: number;
    /**
     * The amount of time that passes in a single simulation step in units of days (d). Negative timestep to simulate backwards in time. Altering the timestep of a running simulation breaks time-reversability!
     */
    readonly timeStep: number;
    /**
     * The total number of bodies in the simulation, including disabled ones.
     */
    readonly bodyCount: number;
    /**
     * The opening-angle parameter (theta, θ) for the Barnes-Hut algorithm. Clamped between 0 and 1.
     */
    readonly theta: number;
    /**
     * The value for the gravitational constant G in m³/kg/s²
     */
    readonly gravitationalConstant: number;
    /**
     * The softening factor (epsilon, ε) used to prevent numerical instability in the simulation. Clamped to a value greater than 0.001.
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
     * The mass of the body in units of Solar Mass (M☉)
     */
    readonly mass: number;
    /**
     * The x position of the body in units of Astronomical Units (au)
     */
    readonly posX: number;
    /**
     * The y position of the body in units of Astronomical Units (au)
     */
    readonly posY: number;
    /**
     * The body's velocity in the x direction in units of Astronomical Units per day (au/d)
     */
    readonly velX: number;
    /**
     * The body's velocity in the y direction in units of Astronomical Units per day (au/d)
     */
    readonly velY: number;
    /**
     * The body's acceleration in the x direction in units of Astronomical Units per day squared (au/d²)
     */
    readonly accX: number;
    /**
     * The body's acceleration in the y direction in units of Astronomical Units per day squared (au/d²)
     */
    readonly accY: number;
    /**
     * Is the body considered to be out of bounds? An out of bounds body is automatically disabled.
     */
    readonly outOfBounds: number;
}