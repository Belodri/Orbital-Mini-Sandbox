namespace Bridge;
#pragma warning disable IDE1006 // Naming Styles

/* 
    Defines the memory layout structures as strongly-typed records. These records are the
    Single Source of Truth for the entire shared memory layout across both C# and JavaScript.

    IMPORTANT:
    - Property names must be written in camelCase! as they define the names of the properties in JavaScript.

    - All properties must be of type 'int'! The system relies on this, as the properties will hold the 
        integer index/offset for each field within the shared memory array.

    - To mark a property as internal (not exposed publicly by Bridge.mjs) it must begin with an underscore.

    - All non-internal properties must have a comment explaining the property's function.
        This comment is used to create the LayoutRecords.d.ts file.
*/

public record SimStateLayoutRec(
    int _bodyBufferPtr,
    int _bodyBufferSize,
    /// <summary>
    /// Internal simulation time in units of days (d).
    /// </summary>
    int simulationTime,
    /// <summary>
    /// A multiplier for how much the simulationTime advances during each simulation tick. Default = 1; Min = 0.001; Max = 1000;
    /// </summary>
    int timeScale,
    /// <summary>
    /// Determines direction of simulationTime. Forward = 1; Backward = 0; 
    /// </summary>
    int timeIsForward,
    /// <summary>
    /// The conversion factor for simulation time (in d) to real time (in s). Default is 1. Must be positive.
    /// </summary>
    int timeConversionFactor,
    /// <summary>
    /// The total number of bodies in the simulation, including disabled ones.
    /// </summary>
    int bodyCount,
    /// <summary>
    /// The opening-angle parameter (theta, θ) for the Barnes-Hut algorithm. Clamped between 0 and 1.
    /// </summary>
    int theta,
    /// <summary>
    /// The value for the gravitational constant G in m³/kg/s²
    /// </summary>
    int gravitationalConstant,
    /// <summary>
    /// The softening factor (epsilon, ε) used to prevent numerical instability in the simulation. Clamped to a value greater than 0.001.
    /// </summary>
    int epsilon
);

public record BodyStateLayoutRec(
    /// <summary>
    /// The unique identifier of the body
    /// </summary>
    int id,
    /// <summary>
    /// Disabled bodies are ignored by the simulation. Enabled = 1; Disabled = 0;
    /// </summary>
    int enabled,
    /// <summary>
    /// The mass of the body in units of Solar Mass (M☉)
    /// </summary>
    int mass,
    /// <summary>
    /// The x position of the body in units of Astronomical Units (au)
    /// </summary>
    int posX,
    /// <summary>
    /// The y position of the body in units of Astronomical Units (au)
    /// </summary>
    int posY,
    /// <summary>
    /// The body's velocity in the x direction in units of Astronomical Units per day (au/d)
    /// </summary>
    int velX,
    /// <summary>
    /// The body's velocity in the y direction in units of Astronomical Units per day (au/d)
    /// </summary>
    int velY,
    /// <summary>
    /// The body's acceleration in the x direction in units of Astronomical Units per day squared (au/d²)
    /// </summary>
    int accX,
    /// <summary>
    /// The body's acceleration in the y direction in units of Astronomical Units per day squared (au/d²)
    /// </summary>
    int accY
);
