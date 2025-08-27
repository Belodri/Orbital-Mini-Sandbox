using Physics;
using Physics.Bodies;
using Physics.Core;
using ITimer = Physics.Core.ITimer;
using Timer = Physics.Core.Timer;
using System.Reflection;

namespace PhysicsTests;

internal static class TestHelpers
{
    public static ICalculator Calculator_New => new Calculator();
    public static ITimer Timer_New => new Timer();
    public static QuadTree QuadTree_New => new();
    public static ISimulation Simulation_New => new Simulation(
        timer: new Timer(),
        quadTree: new(),
        calculator: new Calculator(),
        bodyManager: new BodyManager()
    );
    public static ICelestialBody CelestialBody_New => new CelestialBody(0);

    public static readonly SimDataBase SimDataBase_Preset = new(
        SimulationTime: 10,
        TimeStep: 1,
        Theta: 0.5,
        G_SI: 6.6743e-11,
        Epsilon: 0.001
    );

    public static readonly BodyDataBase BodyDataBase_Preset = new(
        Id: 1,
        Enabled: true,
        Mass: 10,
        PosX: 1.1,
        PosY: 1.2,
        VelX: 1.3,
        VelY: 1.4,
        AccX: 1.5,
        AccY: 1.6
    );

    public static readonly SimDataBase SimDataBase_Default = Simulation_New.ToSimDataBase();
    public static readonly BodyDataBase BodyDataBase_Default = CelestialBody_New.ToBodyDataBase();
    public static BodyDataBase GetBodyDataBase(int id) => new(
        Id: id,
        Enabled: true,
        Mass: id * 10,
        PosX: id + 0.1,
        PosY: id + 0.2,
        VelX: id + 0.3,
        VelY: id + 0.4,
        AccX: id + 0.5,
        AccY: id + 0.6
    );

    public static ICelestialBody CreateBody(BodyDataBase baseData) => baseData.ToCelestialBody();
    public static ICelestialBody CreateBody(int id) => new CelestialBody(id);

    /// <summary>
    /// Uses reflection to get the value of a private field from an object.
    /// This is useful in unit testing to verify internal state without
    /// exposing private fields in the public API.
    /// </summary>
    public static T? GetPrivateField<T>(object obj, string fieldName) where T : class
    {
        var field = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        if (field == null)
        {
            Assert.Fail($"Private field '{fieldName}' not found on type '{obj.GetType().Name}'.");
            return null; // Will not be reached due to Assert.Fail
        }
        return field.GetValue(obj) as T;
    }
}
