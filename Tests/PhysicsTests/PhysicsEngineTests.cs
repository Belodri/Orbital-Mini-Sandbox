using Physics;
using static PhysicsTests.TestHelpers;

namespace PhysicsTests;

[TestFixture]
[DefaultFloatingPointTolerance(1e-12)]
public class PhysicsEngineTests
{
    public IPhysicsEngine _physicsEngine = null!;

    [SetUp]
    public void BeforeEachTest()
    {
        _physicsEngine = new PhysicsEngine();
    }

    #region Preset Tests

    [Test(Description = "Verifies GetBaseData on a new engine returns a default state.")]
    public void GetBaseData_OnNewEngine_ReturnsDefaultState()
    {
        // Act
        var (simData, bodies) = _physicsEngine.GetBaseData();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(simData.SimulationTime, Is.EqualTo(DefaultTimer.SimulationTime));
            Assert.That(simData.TimeScale, Is.EqualTo(DefaultTimer.TimeScale));
            Assert.That(simData.IsTimeForward, Is.EqualTo(DefaultTimer.IsTimeForward));
            Assert.That(simData.TimeConversionFactor, Is.EqualTo(DefaultTimer.TimeConversionFactor));
            Assert.That(simData.Theta, Is.EqualTo(DefaultCalculator.Theta));
            Assert.That(simData.Epsilon, Is.EqualTo(DefaultCalculator.Epsilon));
            Assert.That(simData.GravitationalConstant, Is.EqualTo(DefaultCalculator.GravitationalConstant));
            Assert.That(bodies, Is.Not.Null, "Body list should not be null.");
            Assert.That(bodies, Is.Empty, "Body list should be empty for a new simulation.");
        });
    }

    [Test(Description = "Ensures that data loaded into the engine can be retrieved accurately.")]
    public void Load_And_GetBaseData_Roundtrip()
    {
        // Arrange
        // Create a known state to load.
        var expectedSimData = SimDataBasePreset1;
        var expectedBodies = new List<BodyDataBase>
        {
            GetBodyDataBase(1),
            GetBodyDataBase(2)
        };

        // Act
        _physicsEngine.Load(expectedSimData, expectedBodies);
        var (actualSimData, actualBodies) = _physicsEngine.GetBaseData();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(actualSimData, Is.EqualTo(expectedSimData), "The simulation data should match the loaded state.");
            Assert.That(actualBodies, Is.EquivalentTo(expectedBodies), "The list of bodies should match the loaded state.");
        });
    }


    [Test(Description = "Ensures that loading a new state completely replaces a pre-existing state.")]
    public void Load_OverwritesExistingState()
    {
        // Arrange
        // 1. Create an initial state with 5 bodies.
        for (int i = 0; i < 5; i++) _physicsEngine.CreateBody();
        var (initialSimData, initialBodies) = _physicsEngine.GetBaseData();
        Assert.That(initialBodies, Has.Count.EqualTo(5), "Precondition failed: Initial simulation should have 5 bodies.");

        // Create a new, different state with only 2 bodies.
        var overwriteSimData = SimDataBasePreset1;
        var overwriteBodies = new List<BodyDataBase>
        {
            GetBodyDataBase(3),
            GetBodyDataBase(4)
        };

        // Act
        _physicsEngine.Load(overwriteSimData, overwriteBodies);

        // Assert
        var (finalSimData, finalBodies) = _physicsEngine.GetBaseData();
        Assert.Multiple(() =>
        {
            Assert.That(finalBodies, Has.Count.EqualTo(2), "The number of bodies should be equal to the new state after loading.");
            Assert.That(finalSimData, Is.EqualTo(overwriteSimData), "The simulation data should match the new state.");
            Assert.That(finalBodies, Is.EquivalentTo(overwriteBodies), "The final body data should match the new state.");
        });
    }

    [Test(Description = "Verifies that loading with an empty body list successfully removes all bodies from the simulation.")]
    public void Load_WithEmptyBodyList_ClearsAllBodies()
    {
        // Arrange
        // 1. Create an initial state with 5 bodies.
        for (int i = 0; i < 5; i++) _physicsEngine.CreateBody();
        Assert.That(_physicsEngine.GetBaseData().bodies, Is.Not.Empty, "Precondition failed: Initial simulation should not be empty.");

        // 2. Create a new state with an empty body list.
        var emptyBodySimData = SimDataBasePreset2;
        var emptyBodyList = new List<BodyDataBase>();

        // Act
        _physicsEngine.Load(emptyBodySimData, emptyBodyList);

        // Assert
        var (finalSimData, finalBodies) = _physicsEngine.GetBaseData();
        Assert.Multiple(() =>
        {
            Assert.That(finalBodies, Is.Empty, "The final body list should be empty.");
            Assert.That(finalSimData, Is.EqualTo(emptyBodySimData), "The simulation data should be updated even when clearing bodies.");
        });
    }

    #endregion


    #region Body Management Tests

    [Test(Description = "Verifies that CreateBody adds a body and returns a unique ID.")]
    public void CreateBody_IncreasesBodyCount_And_ReturnsUniqueId()
    {
        // Arrange
        var initialBodyCount = _physicsEngine.GetBaseData().bodies.Count;

        // Act
        int id1 = _physicsEngine.CreateBody();
        int id2 = _physicsEngine.CreateBody();
        var finalBodyCount = _physicsEngine.GetBaseData().bodies.Count;

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
        var initialBodyCount = _physicsEngine.GetBaseData().bodies.Count;

        // Act
        bool result = _physicsEngine.DeleteBody(idToDelete);
        var (_, finalBodies) = _physicsEngine.GetBaseData();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.True, "DeleteBody should return true for an existing body.");
            Assert.That(finalBodies, Has.Count.EqualTo(initialBodyCount - 1), "Body count should decrease by 1.");
            Assert.That(finalBodies.Any(b => b.Id == idToDelete), Is.False, "The deleted body should no longer exist in the simulation.");
        });
    }

    [Test(Description = "Verifies DeleteBody returns false for a non-existent ID and does not alter the simulation.")]
    public void DeleteBody_OnNonExistentId_ReturnsFalse()
    {
        // Arrange
        _physicsEngine.Load(SimDataBasePreset1, [GetBodyDataBase(1)]);
        var initialBodyCount = _physicsEngine.GetBaseData().bodies.Count;
        int nonExistentId = 999;

        // Act
        bool result = _physicsEngine.DeleteBody(nonExistentId);
        var finalBodyCount = _physicsEngine.GetBaseData().bodies.Count;

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
        var initialBody = GetBodyDataBase(1);
        _physicsEngine.Load(SimDataBasePreset1, [initialBody]);

        // Act
        var updates = new BodyDataUpdates(
            Mass: 200,      // Change
            PosX: null,     // Do not change
            PosY: 20,       // Change
            VelX: null      // Do not change
        );
        bool result = _physicsEngine.UpdateBody(initialBody.Id, updates);

        // Assert
        var (_, bodies) = _physicsEngine.GetBaseData();
        var updatedBody = bodies.First(b => b.Id == initialBody.Id);
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.True);
            Assert.That(updatedBody.Mass, Is.EqualTo(200), "Mass should be updated.");
            Assert.That(updatedBody.PosY, Is.EqualTo(20), "PosY should be updated.");
            Assert.That(updatedBody.PosX, Is.EqualTo(initialBody.PosX), "PosX should NOT be updated.");
            Assert.That(updatedBody.VelX, Is.EqualTo(initialBody.VelX), "VelX should NOT be updated.");
        });
    }

    #endregion

    #region Simulation Management Tests
    
    [Test(Description = "Verifies UpdateSimulation updates only non-null properties.")]
    public void UpdateSimulation_WithPartialData_UpdatesCorrectly()
    {
        // Arrange
        var initialSimData = SimDataBasePreset1;
        _physicsEngine.Load(initialSimData, []);

        // Act
        var updates = new SimDataUpdates(
            TimeScale: 2.0,                  // Change
            IsTimeForward: null,             // Do not change
            Theta: 0.99,                     // Change
            IntegrationAlgorithm: null       // Do not change
        );
        _physicsEngine.UpdateSimulation(updates);

        // Assert
        var (finalSimData, _) = _physicsEngine.GetBaseData();
        Assert.Multiple(() =>
        {
            Assert.That(finalSimData.TimeScale, Is.EqualTo(2.0), "TimeScale should be updated.");
            Assert.That(finalSimData.Theta, Is.EqualTo(0.99), "Theta should be updated.");
            Assert.That(finalSimData.IsTimeForward, Is.EqualTo(initialSimData.IsTimeForward), "IsTimeForward should NOT be updated.");
            Assert.That(finalSimData.IntegrationAlgorithm, Is.EqualTo(initialSimData.IntegrationAlgorithm), "IntegrationAlgorithm should NOT be updated.");
        });
    }

    #endregion


    #region Tick Tests

    [Test(Description = "Verifies that Tick advances simulation time and updates body positions.")]
    public void Tick_AdvancesSimulation_And_UpdatesBodyPositions()
    {
        // Arrange
        // The data for this test is specific to the scenario and kept explicit for clarity.
        var simData = new SimDataBase(0, 1, true, 3600, 0.5, 6.674e-11, 0.01, IntegrationAlgorithm.SymplecticEuler);
        var bodiesData = new List<BodyDataBase>
        {
            // A body with some initial velocity
            new(1, true, 1e12, 0, 0, 100, 0, 0, 0),
            // A stationary body to exert gravity
            new(2, true, 1e24, 1e6, 1e6, 0, 0, 0, 0)
        };
        _physicsEngine.Load(simData, bodiesData);

        var (t0Sim, t0Bodies) = _physicsEngine.GetFullData();
        var body1AtT0 = t0Bodies.First(b => b.Id == 1);

        // Act
        _physicsEngine.Tick(100); // Simulate 100ms of real time

        // Assert
        var (t1Sim, t1Bodies) = _physicsEngine.GetFullData();
        var body1AtT1 = t1Bodies.First(b => b.Id == 1);
        Assert.Multiple(() =>
        {
            Assert.That(t1Sim.SimulationTime, Is.GreaterThan(t0Sim.SimulationTime), "SimulationTime should advance after a tick.");
            Assert.That(body1AtT1.PosX, Is.Not.EqualTo(body1AtT0.PosX), "Body1's X position should change after a tick due to velocity and gravity.");
            Assert.That(body1AtT1.PosY, Is.Not.EqualTo(body1AtT0.PosY), "Body1's Y position should change after a tick due to gravity.");
        });
    }

    #endregion
}
