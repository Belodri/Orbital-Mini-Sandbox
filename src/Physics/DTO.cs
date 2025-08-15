namespace Physics;

#region BodyData DTOs

/// <summary>
/// The base data that defines a celestial body.
/// </summary>
public record BodyDataBase(int Id, bool Enabled, double Mass, double PosX, double PosY, double VelX, double VelY, double AccX, double AccY);

/// <summary>
/// Partial data to update a celestial body. Null values are ignored. 
/// </summary>
public record BodyDataUpdates(
    bool? Enabled = null,
    double? Mass = null,
    double? PosX = null,
    double? PosY = null,
    double? VelX = null,
    double? VelY = null,
    double? AccX = null,
    double? AccY = null
);

#endregion


#region SimData DTOs

/// <summary>
/// The base data that defines a simulation.
/// </summary>
public record SimDataBase(
    // Timer
    double SimulationTime, double TimeStep,
    // Calculator
    double Theta, double G_SI, double Epsilon
);

/// <summary>
/// Partial data to update a simulation. Null values are ignored. 
/// </summary>
public record SimDataUpdates(
    double? TimeStep = null,
    double? Theta = null,
    double? G_SI = null,
    double? Epsilon = null
);

#endregion
