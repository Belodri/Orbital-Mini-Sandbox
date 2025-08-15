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
            timeStep: data.TimeStep
        );

    internal static Calculator ToCalculator(this SimDataBase data)
        => new(
            g_SI: data.G_SI,
            theta: data.Theta,
            epsilon: data.Epsilon
        );

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
    
    #endregion

    #region Sim => Data

    internal static SimDataBase ToSimDataBase(this ISimulation sim)
        => new(
            SimulationTime: sim.Timer.SimulationTime,
            TimeStep: sim.Timer.TimeStep,
            Theta: sim.Calculator.Theta,
            G_SI: sim.Calculator.G_SI,
            Epsilon: sim.Calculator.Epsilon
        );

    #endregion
}
