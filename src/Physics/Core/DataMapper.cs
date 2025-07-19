using Physics.Bodies;

namespace Physics.Core;

internal static class DataMapper
{
    #region Base Data => Instances

    internal static CelestialBody ToCelestialBody(this BodyDataBase data)
        => new(
            id: data.Id,
            enabled: data.Enabled,
            mass: data.Mass,
            position: new(data.PosX, data.PosY),
            velocity: new(data.VelX, data.VelY),
            acceleration: new(data.AccX, data.AccY)
        );

    internal static Timer ToTimer(this SimDataBase data)
        => new(
            simulationTime: data.SimulationTime,
            timeScale: data.TimeScale,
            isTimeForward: data.IsTimeForward,
            timeConversionFactor: data.TimeConversionFactor
        );

    internal static Calculator ToCalculator(this SimDataBase data)
        => new(
            gravitationalConstant: data.GravitationalConstant,
            theta: data.Theta,
            epsilon: data.Epsilon,
            algorithm: data.IntegrationAlgorithm
        );

    internal static Grid ToGrid(this SimDataBase data)
        => new();

    #endregion

    #region Body => Data

    internal static BodyDataBase ToBodyDataBase(this ICelestialBody body)
        => new(
            Id: body.Id,
            Enabled: body.Enabled,
            Mass: body.Mass,
            PosX: body.Position.X,
            PosY: body.Position.Y,
            VelX: body.Velocity.X,
            VelY: body.Velocity.Y,
            AccX: body.Acceleration.X,
            AccY: body.Acceleration.Y
        );
    
    internal static BodyDataFull ToBodyDataFull(this ICelestialBody body)
        => new(
            Id: body.Id,
            Enabled: body.Enabled,
            Mass: body.Mass,
            PosX: body.Position.X,
            PosY: body.Position.Y,
            VelX: body.Velocity.X,
            VelY: body.Velocity.Y,
            AccX: body.Acceleration.X,
            AccY: body.Acceleration.Y
        );

    #endregion

    #region Sim => Data

    internal static SimDataBase ToSimDataBase(this ISimulation sim)
        => new(
            SimulationTime: sim.Timer.SimulationTime,
            TimeScale: sim.Timer.TimeScale,
            IsTimeForward: sim.Timer.IsTimeForward,
            TimeConversionFactor: sim.Timer.TimeConversionFactor,
            Theta: sim.Calculator.Theta,
            GravitationalConstant: sim.Calculator.GravitationalConstant,
            Epsilon: sim.Calculator.Epsilon,
            IntegrationAlgorithm: sim.Calculator.IntegrationAlgorithm
        );

    internal static SimDataFull ToSimDataFull(this ISimulation sim)
        => new(
            SimulationTime: sim.Timer.SimulationTime,
            TimeScale: sim.Timer.TimeScale,
            IsTimeForward: sim.Timer.IsTimeForward,
            TimeConversionFactor: sim.Timer.TimeConversionFactor,
            Theta: sim.Calculator.Theta,
            GravitationalConstant: sim.Calculator.GravitationalConstant,
            Epsilon: sim.Calculator.Epsilon,
            IntegrationAlgorithm: sim.Calculator.IntegrationAlgorithm
        );

    #endregion
}
