using Bridge;
using Physics;
using System.Text.Json;
using NUnit.Framework.Internal;

#pragma warning disable CA1416 // Validate platform compatibility

namespace BridgeTests;

[TestFixture]
public partial class PresetTests
{
    readonly SimDataBase simBase = new(
        SimulationTime: 123.45,
        TimeStep: 1,
        Theta: 0.7,
        G_SI: 7.89e12,
        Epsilon: 0.023
    );

    readonly BodyDataBase bodyBase = new(
        Id: 4,
        Enabled: true,
        Mass: 1,
        PosX: 2,
        PosY: 3,
        VelX: 4,
        VelY: 5,
        AccX: 6,
        AccY: 7
    );

    [SetUp]
    public static void ResetPhysics() => EngineBridge.physicsEngine = new();

    [Test]
    public void RoundTrip_ResultsInEquivalentState()
    {
        // Initial Setup
        EngineBridge.physicsEngine.Import(simBase, [bodyBase]);
        var presetInitial = EngineBridge.GetPreset();

        // Verify Correct State loaded
        var view = EngineBridge.physicsEngine.View;

        Assert.Multiple(() =>
        {
            // Set values - Sim
            Assert.That(view.SimulationTime, Is.EqualTo(simBase.SimulationTime));
            Assert.That(view.Theta, Is.EqualTo(simBase.Theta));
            Assert.That(view.TimeStep, Is.EqualTo(simBase.TimeStep));
            Assert.That(view.G_SI, Is.EqualTo(simBase.G_SI));
            Assert.That(view.Epsilon, Is.EqualTo(simBase.Epsilon));

            // Set values - Body
            Assert.That(view.Bodies, Has.Count.EqualTo(1));

            var bodyView = view.Bodies[0];
            Assert.That(bodyView.Id, Is.EqualTo(bodyBase.Id));
            Assert.That(bodyView.Enabled, Is.EqualTo(bodyBase.Enabled));
            Assert.That(bodyView.Mass, Is.EqualTo(bodyBase.Mass));
            Assert.That(bodyView.Position.X, Is.EqualTo(bodyBase.PosX));
            Assert.That(bodyView.Position.Y, Is.EqualTo(bodyBase.PosY));
            Assert.That(bodyView.Velocity.X, Is.EqualTo(bodyBase.VelX));
            Assert.That(bodyView.Velocity.Y, Is.EqualTo(bodyBase.VelY));
            Assert.That(bodyView.Acceleration.X, Is.EqualTo(bodyBase.AccX));
            Assert.That(bodyView.Acceleration.Y, Is.EqualTo(bodyBase.AccY));
        });

        // Change state
        var simUpdates = new SimDataUpdates(
            Theta: 0.1,
            TimeStep: 4
        );

        var bodyUpdates = new BodyDataUpdates(
            Mass: -1,
            PosX: 1,
            VelY: 0
        );

        EngineBridge.physicsEngine.UpdateSimulation(simUpdates);
        EngineBridge.physicsEngine.UpdateBody(id: 4, bodyUpdates);

        Assert.Multiple(() =>
        {
            // Updated values - Sim
            Assert.That(view.Theta, Is.EqualTo(simUpdates.Theta));
            Assert.That(view.TimeStep, Is.EqualTo(simUpdates.TimeStep));

            // Non-updated values - Sim
            Assert.That(view.SimulationTime, Is.EqualTo(simBase.SimulationTime));
            Assert.That(view.G_SI, Is.EqualTo(simBase.G_SI));

            // Updated values - Body
            Assert.That(view.Bodies, Has.Count.EqualTo(1));

            var bodyView = view.Bodies[0];
            Assert.That(bodyView.Mass, Is.EqualTo(bodyUpdates.Mass));       // changed
            Assert.That(bodyView.Position.X, Is.EqualTo(bodyUpdates.PosX)); // changed
            Assert.That(bodyView.Position.Y, Is.EqualTo(bodyBase.PosY));    // unchanged
            Assert.That(bodyView.Velocity.Y, Is.EqualTo(bodyUpdates.VelY)); // changed

            // Get preset and verify its difference to the initial
            var presetPostChange = EngineBridge.GetPreset();
            Assert.That(presetPostChange, Is.Not.EqualTo(presetInitial));
        });

        // Load the initial preset back in
        EngineBridge.LoadPreset(presetInitial);

        // Verify the state is correct as it 
        Assert.Multiple(() =>
        {
            // Set values - Sim
            Assert.That(view.SimulationTime, Is.EqualTo(simBase.SimulationTime));
            Assert.That(view.Theta, Is.EqualTo(simBase.Theta));
            Assert.That(view.TimeStep, Is.EqualTo(simBase.TimeStep));
            Assert.That(view.G_SI, Is.EqualTo(simBase.G_SI));
            Assert.That(view.Epsilon, Is.EqualTo(simBase.Epsilon));

            // Set values - Body
            Assert.That(view.Bodies, Has.Count.EqualTo(1));

            var bodyView = view.Bodies[0];
            Assert.That(bodyView.Id, Is.EqualTo(bodyBase.Id));
            Assert.That(bodyView.Enabled, Is.EqualTo(bodyBase.Enabled));
            Assert.That(bodyView.Mass, Is.EqualTo(bodyBase.Mass));
            Assert.That(bodyView.Position.X, Is.EqualTo(bodyBase.PosX));
            Assert.That(bodyView.Position.Y, Is.EqualTo(bodyBase.PosY));
            Assert.That(bodyView.Velocity.X, Is.EqualTo(bodyBase.VelX));
            Assert.That(bodyView.Velocity.Y, Is.EqualTo(bodyBase.VelY));
            Assert.That(bodyView.Acceleration.X, Is.EqualTo(bodyBase.AccX));
            Assert.That(bodyView.Acceleration.Y, Is.EqualTo(bodyBase.AccY));
        });
    }

    [Test]
    public void LoadPreset_WithJsonNullLiteral_ThrowsException()
    {
        Assert.Throws<ArgumentException>(() => EngineBridge.LoadPreset("null"));
    }

    [Test]
    public void ParseJsonPreset_WithJsonNullLiteral_ReturnsNull()
    {
        var result = EngineBridge.ParseJsonPreset("null");
        Assert.That(result, Is.Null, "Parsing the JSON 'null' literal should result in a null object.");
    }

    [Test]
    public void ParseJsonPreset_MalformedJson_ThrowsException()
    {
        // Initial Setup
        EngineBridge.physicsEngine.Import(simBase, [bodyBase]);
        var presetInitial = EngineBridge.GetPreset();

        // Modify preset string
        var invalidPreset_malformedJson = presetInitial + ",";
        var invalidPreset_typeMismatch = presetInitial.Replace("\"enabled\":true", "\"enabled\":\"true\""); // string instead of bool

        Assert.Multiple(() =>
        {
            Assert.Throws<JsonException>(() => EngineBridge.ParseJsonPreset(invalidPreset_malformedJson));
            Assert.Throws<JsonException>(() => EngineBridge.ParseJsonPreset(invalidPreset_typeMismatch));
        });
    }


}
