using Physics.Models;

namespace Physics;

public record BodyStateData(int Id, bool Enabled, double Mass, double PosX, double PosY, double VelX, double VelY);

public record SimStateData(double SimulationTime, double TimeScale, bool IsTimeForward);

public record TickDataDto(SimStateData SimStateData, BodyStateData[] BodiesStateData);


public class PhysicsEngine
{
    internal Simulation Simulation = new();


    #region Public Methods

    public TickDataDto Tick(double timestamp)
    {
        // TESTING ONLY
        return CreateTickDataDto();
    }

    public void CreateTestSim(int bodyCount)
    {
        Simulation sim = new()
        {
            TimeScale = 1.0,
            SimulationTime = 0.0,
            IsTimeForward = true
        };
        for (int i = 0; i < bodyCount; i++) sim.CreateNewBody();
        Simulation = sim;
    }

    public BodyStateData? GetBodyStateData(int id)
    {
        return Simulation.Bodies.TryGetValue(id, out var body)
            ? ExtractBodyStateData(body)
            : null;
    }

    public SimStateData GetSimStateData => ExtractSimStateData(Simulation);

    #endregion

    private TickDataDto CreateTickDataDto()
    {
        BodyStateData[] BodiesStateData = [.. Simulation.Bodies.Values.Select(ExtractBodyStateData)];
        SimStateData SimStateData = ExtractSimStateData(Simulation);
        return new(SimStateData, BodiesStateData);
    }

    private static BodyStateData ExtractBodyStateData(CelestialBody body) {
        return new(
            body.Id,
            body.Enabled,
            body.Mass,
            body.Position.X,
            body.Position.Y,
            body.Velocity.X,
            body.Velocity.Y
        );
    }

    private static SimStateData ExtractSimStateData(Simulation sim)
    {
        return new(
            sim.SimulationTime,
            sim.TimeScale,
            sim.IsTimeForward
        );
    }
}



internal class CelestialBody(int Id, bool Enabled, double Mass, Vector2D Position, Vector2D Velocity)
{
    internal int Id { get; init; } = Id;
    internal bool Enabled { get; set; } = Enabled;
    internal double Mass { get; set; } = Mass;
    internal Vector2D Position { get; set; } = Position;
    internal Vector2D Velocity { get; set; } = Velocity;

    double PosX => Position.X;
    double PosY => Position.Y;
    double VelX => Velocity.X;
    double VelY => Velocity.Y;

    internal BodyStateData GetBodyStateDataDto()
    {
        return new(Id, Enabled, Mass, PosX, PosY, VelX, VelY);
    }
}

internal class Simulation
{
    internal double SimulationTime { get; set; }
    internal double TimeScale { get; set; }
    internal bool IsTimeForward { get; set; }

    static readonly Random rnd = new();

    internal Dictionary<int, CelestialBody> Bodies = [];

    internal CelestialBody CreateNewBody()
    {
        int ValidId = -1;
        while (ValidId < 0)
        {
            int Id = rnd.Next();
            if (!Bodies.ContainsKey(Id)) ValidId = Id;
        }

        //default data; replace with CONFIG later
        var mass = 10.0;
        var enabled = false;    // must be false!
        var pos = new Vector2D(20.0, 30.0);
        var vel = new Vector2D(40.0, 50.0);

        CelestialBody body = new(ValidId, enabled, mass, pos, vel);
        Bodies.Add(body.Id, body);
        return body;
    }
}
