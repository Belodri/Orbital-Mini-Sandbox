using Physics;
using Physics.Bodies;
using Physics.Core;
using ITimer = Physics.Core.ITimer;
using Timer = Physics.Core.Timer;
using System.Reflection;

namespace PhysicsTests;

internal static class TestHelpers
{
    public static readonly SimDataBase SimDataBasePreset1 = new(123.45, 1.5, true, 123456, 0.5, 7.89e-11, 0.01, IntegrationAlgorithm.SymplecticEuler);
    public static readonly SimDataBase SimDataBasePreset2 = new(1000, 1, false, 6000000, 0.8, 1, 1, IntegrationAlgorithm.VelocityVerlet);

    public static readonly SimDataFull SimDataFullPreset1 = new(123.45, 1.5, true, 123456, 0.5, 7.89e-11, 0.01, IntegrationAlgorithm.SymplecticEuler);
    public static readonly SimDataFull SimDataFullPreset2 = new(1000, 1, false, 6000000, 0.8, 1, 1, IntegrationAlgorithm.VelocityVerlet);

    public static readonly BodyDataBase BodyDataBasePreset = new(1, true, 10, 1.1, 1.2, 1.3, 1.4, 1.5, 1.6);
    public static readonly BodyDataBase BodyDataBasePresetInvalid = new(-1, true, 10, 1.1, 1.2, 1.3, 1.4, 1.5, 1.6);    // negaive body id is invalid

    public static readonly BodyDataFull BodyDataFullPreset = new(1, true, 10, 1.1, 1.2, 1.3, 1.4, 1.5, 1.6);
    public static readonly BodyDataFull BodyDataFullPresetInvalid = new(-1, true, 10, 1.1, 1.2, 1.3, 1.4, 1.5, 1.6);    // negaive body id is invalid

    public static readonly ICalculator DefaultCalculator = new Calculator();
    public static readonly ITimer DefaultTimer = new Timer();
    public static readonly IGrid DefaultGrid = new Grid();
    public static readonly ISimulation DefaultSimulation = new Simulation(
        timer: new Timer(),
        grid: new Grid(),
        calculator: new Calculator()
    );
    public static readonly CelestialBody DefaultCelestialBody = new(0);

    public static readonly SimDataBase DefaultSimDataBase = DefaultSimulation.ToSimDataBase();
    public static readonly SimDataFull DefaultSimDataFull = DefaultSimulation.ToSimDataFull();

    public static readonly BodyDataBase DefaultBodyDataBase = DefaultCelestialBody.ToBodyDataBase();
    public static readonly BodyDataFull DefaultBodyDataFull = DefaultCelestialBody.ToBodyDataFull();

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

    public static CelestialBody CreateBody(BodyDataBase baseData) => baseData.ToCelestialBody();

    public static CelestialBody CreateBody(int id) => new (id);

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
