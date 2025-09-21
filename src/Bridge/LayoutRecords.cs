namespace Bridge;
#pragma warning disable IDE1006 // Naming Styles

/* 
    Defines the memory layout structures as strongly-typed records. These records are the
    Single Source of Truth for the entire shared memory layout across both C# and JavaScript.

    ########################################################################################

    FAILURE TO ADHERE TO THESE CONSTRAINTS WILL CAUSE FATAL ERRORS DURING BUILD OR RUNTIME

    Records must
    - be public
    - be flagged with `[Attributes.GenerateLayoutRecord]`
    - consist of nothing but a primary constructor
    - not extend or inherit anything
    - not have type parameters

    Record constructor params must
    - be of types `int`, `double`, or `bool`
    - have names written in camelCase
    - must have an xml comment explaining the parameter

    ##########################################################################################

    DETAILS

    Constructor parameter names must be written in camelCase
    as they define the names of the properties in JavaScript.

    TypeScript types are generated from these records, 
    and the self-configuring runtime layout uses them to know which values to cast.
    The parameter comments are included in the generated LayoutRecords.d.ts file.

    The [Attributes.GenerateLayoutRecord] flags a record to a custom source generation script, 
    which generates alternate versions, which have Rec" appended to the name.

    These generated records have identical parameter names, but all parameter types are set to `int`.
    This is used by the C# writer to determine, hold, and efficiently access
    the integer index/offset for each field within the shared memory array.

    Example: 
    ```
    [Attributes.GenerateLayoutRecord]
    public record Initial(bool bar)

    // source generator output
    public record InitialRec(int bar)
    ```
*/


[Attributes.GenerateLayoutRecord]
public record SimStateLayout(
    /// <summary>
    /// Internal simulation time in units of days (d).
    /// </summary>
    int simulationTime,
    /// <summary>
    /// The amount of time that passes in a single simulation step in units of days (d). Negative timestep to simulate backwards in time.
    /// </summary>
    double timeStep,
    /// <summary>
    /// The total number of bodies in the simulation, including disabled ones.
    /// </summary>
    int bodyCount,
    /// <summary>
    /// The opening-angle parameter (theta, θ) for the Barnes-Hut algorithm. Clamped between 0 and 1.
    /// </summary>
    double theta,
    /// <summary>
    /// The value for the gravitational constant G in m³/kg/s²
    /// </summary>
    double gravitationalConstant,
    /// <summary>
    /// The softening factor (epsilon, ε) used to prevent numerical instability in the simulation. Clamped to a value greater than 0.001.
    /// </summary>
    double epsilon
);

[Attributes.GenerateLayoutRecord]
public record BodyStateLayout(
    /// <summary>
    /// The unique identifier of the body
    /// </summary>
    int id,
    /// <summary>
    /// Disabled bodies are ignored by the simulation.
    /// </summary>
    bool enabled,
    /// <summary>
    /// The mass of the body in units of Solar Mass (M☉)
    /// </summary>
    double mass,
    /// <summary>
    /// The x position of the body in units of Astronomical Units (au)
    /// </summary>
    double posX,
    /// <summary>
    /// The y position of the body in units of Astronomical Units (au)
    /// </summary>
    double posY,
    /// <summary>
    /// The body's velocity in the x direction in units of Astronomical Units per day (au/d)
    /// </summary>
    double velX,
    /// <summary>
    /// The body's velocity in the y direction in units of Astronomical Units per day (au/d)
    /// </summary>
    double velY,
    /// <summary>
    /// The body's acceleration in the x direction in units of Astronomical Units per day squared (au/d²)
    /// </summary>
    double accX,
    /// <summary>
    /// The body's acceleration in the y direction in units of Astronomical Units per day squared (au/d²)
    /// </summary>
    double accY,
    /// <summary>
    /// Is the body considered to be out of bounds? An out of bounds body is automatically disabled.
    /// </summary>
    bool outOfBounds
);
