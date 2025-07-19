using Physics.Models;
namespace Physics;

#region BodyData DTOs

/// <summary>
/// The base data that defines a celestial body.
/// </summary>
public record BodyDataBase(int Id, bool Enabled, double Mass, double PosX, double PosY, double VelX, double VelY, double AccX, double AccY);

/// <summary>
/// The base data that that defines a celestial body plus any derived properties of the body.
/// </summary>
public record BodyDataFull(int Id, bool Enabled, double Mass, double PosX, double PosY, double VelX, double VelY, double AccX, double AccY)
    : BodyDataBase(Id, Enabled, Mass, PosX, PosY, VelX, VelY, AccX, AccY);

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
    double SimulationTime, double TimeScale, bool IsTimeForward, double TimeConversionFactor,
    // Calculator
    double Theta, double GravitationalConstant, double Epsilon, IntegrationAlgorithm IntegrationAlgorithm
);

/// <summary>
/// The base data that defines a simulation plus any derived properties.
/// </summary>
public record SimDataFull(
    double SimulationTime, double TimeScale, bool IsTimeForward, double TimeConversionFactor,
    double Theta, double GravitationalConstant, double Epsilon, IntegrationAlgorithm IntegrationAlgorithm
) : SimDataBase(SimulationTime, TimeScale, IsTimeForward, TimeConversionFactor, Theta, GravitationalConstant, Epsilon, IntegrationAlgorithm);

/// <summary>
/// Partial data to update a simulation. Null values are ignored. 
/// </summary>
public record SimDataUpdates(
    double? TimeScale = null,
    bool? IsTimeForward = null,
    double? TimeConversionFactor = null,
    double? Theta = null,
    double? GravitationalConstant = null,
    double? Epsilon = null,
    IntegrationAlgorithm? IntegrationAlgorithm = null
);

#endregion


#region Other Public Data

public enum IntegrationAlgorithm
{
    SymplecticEuler,
    RungeKutta4,
    VelocityVerlet
}

#endregion


#region Internal DTOs

internal readonly record struct EvaluationResult(Vector2D Position, Vector2D Velocity, Vector2D Acceleration);

#endregion

