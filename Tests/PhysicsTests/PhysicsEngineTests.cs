using System.Numerics;
using Physics;
using Physics.Core;
using Physics.Models;
using static PhysicsTests.TestHelpers;
using Timer = Physics.Core.Timer;

namespace PhysicsTests;

[TestFixture]
[DefaultFloatingPointTolerance(1e-12)]
public class PhysicsEngineTests
{
    public PhysicsEngine _physicsEngine = null!;

    [SetUp]
    public void BeforeEachTest()
    {
        _physicsEngine = new PhysicsEngine();
    }

    #region Import/Export

    [Test(Description = "Verifies Export on a new engine returns a default state.")]
    public void Export_OnNewEngine_ReturnsDefaultState()
    {
        var (simData, bodies) = _physicsEngine.Export();
        Assert.Multiple(() =>
        {
            Assert.That(simData.SimulationTime, Is.EqualTo(Timer.SIMULATION_TIME_DEFAULT));
            Assert.That(simData.TimeStep, Is.EqualTo(Timer.TIME_STEP_DEFAULT));
            Assert.That(simData.Theta, Is.EqualTo(Calculator.THETA_DEFAULT));
            Assert.That(simData.Epsilon, Is.EqualTo(Calculator.EPSILON_DEFAULT));
            Assert.That(simData.G_SI, Is.EqualTo(Calculator.G_SI_DEFAULT));
            Assert.That(bodies, Is.Not.Null, "Body list should not be null.");
            Assert.That(bodies, Is.Empty, "Body list should be empty for a new simulation.");
        });
    }

    [Test(Description = "Ensures that data imported into the engine can be exported accurately.")]
    public void Import_Export_Roundtrip()
    {
        // Arrange
        // Create a known state to load.
        var importSimData = SimDataBase_Preset;
        var importBodies = new List<BodyDataBase>
        {
            GetBodyDataBase(1),
            GetBodyDataBase(2)
        };

        // Act
        _physicsEngine.Import(importSimData, importBodies);
        var (exportSimData, exportBodies) = _physicsEngine.Export();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(exportSimData, Is.EqualTo(importSimData), "The simulation data should match the loaded state.");
            Assert.That(exportBodies, Is.EquivalentTo(importBodies), "The list of bodies should match the loaded state.");
        });
    }


    [Test(Description = "Ensures that importing replaces a pre-existing state.")]
    public void Load_OverwritesExistingState()
    {
        // Arrange
        // Create an initial state with 5 bodies.
        for (int i = 0; i < 5; i++) _physicsEngine.CreateBody();
        var (initialSimData, initialBodies) = _physicsEngine.Export();
        Assert.That(initialBodies, Has.Count.EqualTo(5), "Precondition failed: Initial simulation should have 5 bodies.");

        // Create a new, different state with only 2 bodies.
        var overwriteSimData = SimDataBase_Preset;
        var overwriteBodies = new List<BodyDataBase>
        {
            GetBodyDataBase(3),
            GetBodyDataBase(4)
        };

        // Act
        _physicsEngine.Import(overwriteSimData, overwriteBodies);

        // Assert
        var (finalSimData, finalBodies) = _physicsEngine.Export();
        Assert.Multiple(() =>
        {
            Assert.That(finalBodies, Has.Count.EqualTo(2), "The number of bodies in the simulation should match the number of imported bodies.");
            Assert.That(finalSimData, Is.EqualTo(overwriteSimData), "The simulation data should match the new state.");
            Assert.That(finalBodies, Is.EquivalentTo(overwriteBodies), "The final body data should match the new state.");
        });
    }

    [Test(Description = "Verifies that importing with an empty body list successfully removes all bodies from a previous simulation.")]
    public void Load_WithEmptyBodyList_ClearsAllBodies()
    {
        // Arrange
        // 1. Create an initial state with 5 bodies.
        for (int i = 0; i < 5; i++) _physicsEngine.CreateBody();
        Assert.That(_physicsEngine.Export().bodies, Has.Count.EqualTo(5), "Precondition failed: Initial simulation should have 5 bodies.");

        // 2. Create a new state with an empty body list.
        var overwriteSimData = SimDataBase_Preset;
        var overwriteBodyList = new List<BodyDataBase>();

        // Act
        _physicsEngine.Import(overwriteSimData, overwriteBodyList);

        // Assert
        var (_, finalBodies) = _physicsEngine.Export();
        Assert.That(finalBodies, Is.Empty, "The final body list should be empty.");
    }

    #endregion


    #region Body Management Tests

    [Test(Description = "Verifies that CreateBody adds a body and returns a unique ID.")]
    public void CreateBody_IncreasesBodyCount_And_ReturnsUniqueId()
    {
        // Arrange
        var initialBodyCount = _physicsEngine.Export().bodies.Count;

        // Act
        int id1 = _physicsEngine.CreateBody();
        int id2 = _physicsEngine.CreateBody();
        var finalBodyCount = _physicsEngine.Export().bodies.Count;

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(finalBodyCount, Is.EqualTo(initialBodyCount + 2), "Body count should increase by 2.");
            Assert.That(id1, Is.Not.EqualTo(id2), "Each created body should have a unique ID.");
        });
    }    

    
    [Test(Description = "Verifies DeleteBody removes the correct body and returns true.")]
    public void DeleteBody_OnExistingId_ReturnsTrue_And_DecreasesBodyCount()
    {
        // Arrange
        int idToDelete = _physicsEngine.CreateBody();
        _physicsEngine.CreateBody(); // Create another body to ensure we only delete the correct one.
        var initialBodyCount = _physicsEngine.Export().bodies.Count;

        // Act
        bool result = _physicsEngine.DeleteBody(idToDelete);
        var (_, finalBodies) = _physicsEngine.Export();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.True, "DeleteBody should return true for an existing body.");
            Assert.That(_physicsEngine.View.Bodies, Has.Count.EqualTo(initialBodyCount - 1), "Number of bodies should decrease by 1.");
            Assert.That(_physicsEngine.View.Bodies.Any(b => b.Id == idToDelete), Is.False, "The deleted body should no longer exist in the simulation.");
        });
    }

    [Test(Description = "Verifies DeleteBody returns false for a non-existent ID and does not alter the simulation.")]
    public void DeleteBody_OnNonExistentId_ReturnsFalse()
    {
        // Arrange
        int createdId = _physicsEngine.CreateBody();
        int initialBodyCount = _physicsEngine.View.Bodies.Count;
        int nonExistentId = createdId + 10000;

        // Act
        bool result = _physicsEngine.DeleteBody(nonExistentId);
        var finalBodyCount = _physicsEngine.Export().bodies.Count;

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.False, "DeleteBody should return false for a non-existent ID.");
            Assert.That(finalBodyCount, Is.EqualTo(initialBodyCount), "Body count should not change when deletion fails.");
        });
    }

    [Test(Description = "Verifies UpdateBody updates only non-null properties.")]
    public void UpdateBody_WithPartialData_UpdatesCorrectly()
    {
        // Arrange
        int bodyId = _physicsEngine.CreateBody();

        BodyView liveView = _physicsEngine.View.Bodies.Where(bv => bv.Id == bodyId).FirstOrDefault();

        Vector2D pos_initial = liveView.Position;
        Vector2D vel_initial = liveView.Velocity;
        Vector2D acc_initial = liveView.Acceleration;

        // Act
        var updates = new BodyDataUpdates(
            Mass: 200,
            // Update only one part of the position vector 
            PosX: null,     
            PosY: 20,
            // Update no parts of the velocity vector
            VelX: null,
            VelY: null,
            // Update both parts of the acceleration vector
            AccX: 6,
            AccY: 9      
        );
        bool result = _physicsEngine.UpdateBody(bodyId, updates);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.True);
            Assert.That(liveView.Mass, Is.EqualTo(200), "Mass should be updated.");
            Assert.That(liveView.Position.X, Is.EqualTo(pos_initial.X), "PosX should NOT be updated.");
            Assert.That(liveView.Position.Y, Is.EqualTo(20), "PosY should be updated.");
            Assert.That(liveView.Velocity.X, Is.EqualTo(vel_initial.X), "VelX should NOT be updated.");
            Assert.That(liveView.Velocity.Y, Is.EqualTo(vel_initial.Y), "VelY should NOT be updated.");
            Assert.That(liveView.Acceleration.X, Is.EqualTo(6), "AccX should be updated.");
            Assert.That(liveView.Acceleration.Y, Is.EqualTo(9), "AccY should be updated.");
        });
    }

    #endregion

    #region Simulation Management Tests
    
    [Test(Description = "Verifies UpdateSimulation updates only non-null properties.")]
    public void UpdateSimulation_WithPartialData_UpdatesCorrectly()
    {
        // Arrange
        var initialSimData = SimDataBase_Preset;
        _physicsEngine.Import(initialSimData, []);

        var view = _physicsEngine.View;

        double G_SI_initial = view.G_SI;
        double epsilon_initial = view.Epsilon;

        // Act
        var updates = new SimDataUpdates(
            TimeStep: 2.0,                   // Change
            G_SI: null,
            Theta: 0.99,                     // Change
            Epsilon: null
        );
        _physicsEngine.UpdateSimulation(updates);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(view.TimeStep, Is.EqualTo(2.0), "TimeScale should be updated.");
            Assert.That(view.Theta, Is.EqualTo(0.99), "Theta should be updated.");
            Assert.That(view.G_SI, Is.EqualTo(G_SI_initial), "G_SI should NOT be updated.");
            Assert.That(view.Epsilon, Is.EqualTo(epsilon_initial), "Epsilon should NOT be updated.");
        });
    }

    #endregion


    #region Tick Tests

    [Test(Description = "Verifies that Tick advances simulation time and updates body positions.")]
    public void Tick_AdvancesSimulation_And_UpdatesBodyPositions()
    {
        // Arrange
        // The data for this test is specific to the scenario and kept explicit for clarity.
        var simData = new SimDataBase(
            SimulationTime: 0,
            TimeStep: 1,
            Theta: 0.5,
            G_SI: 6.674e-11,
            Epsilon: 0.01
        );
        var bodiesData = new List<BodyDataBase>
        {
            // A body with some initial velocity
            new(0, true, 1, 0, 0, 100, 0, 0, 0),
            // A stationary body to exert gravity
            new(1, true, 1e5, 1000, 1000, 0, 0, 0, 0)
        };
        _physicsEngine.Import(simData, bodiesData);

        var view = _physicsEngine.View;
        var bodyView0 = view.Bodies[0];
        var bodyView1 = view.Bodies[1];

        // Act
        _physicsEngine.Tick();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(view.SimulationTime, Is.GreaterThan(simData.SimulationTime), "SimulationTime should advance after a tick.");
            Assert.That(bodyView0.Position.X, Is.Not.EqualTo(bodiesData[0].PosX), "Body0's X position should change after a tick due to velocity and gravity.");
            Assert.That(bodyView0.Position.X, Is.Not.EqualTo(bodiesData[0].PosY), "Body0's Y position should change after a tick due to gravity.");
        });
    }

    #endregion
}
