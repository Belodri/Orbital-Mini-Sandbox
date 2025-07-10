using Physics;
using Physics.Bodies;
using Physics.Core;

namespace PhysicsTests;

[TestFixture]
public partial class Tests
{
    // Default [OneTimeSetUp] and [SetUp] methods are in SetupTests.cs
    // They ensure a new PhysicsEngine instance `physicsEngine` is created before
    // each test and can safely be accessed and modified within each test method! 

    [Test(Description = "Verifies a new engine is correctly initialized with with default data.")]
    public void VerifyInitialState()
    {
        var sim = physicsEngine.simulation;
        Assert.Multiple(() =>
        {
            Assert.That(sim.SimulationTime, Is.EqualTo(0.0));
            Assert.That(sim.TimeScale, Is.EqualTo(1.0));
            Assert.That(sim.IsTimeForward, Is.True);
            Assert.That(sim.Bodies, Is.Empty);
        });
    }

    #region Preset Tests

    [Test(Description = "Verifies GetPresetData on a new engine returns a preset that reflects the state of the simulation.")]
    public void GetPresetData_FromInitialState_ReflectsSimulationState()
    {
        // Act
        var presetData = physicsEngine.GetPresetData();
        var sim = physicsEngine.simulation;

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(presetData.PresetSimData.SimulationTime, Is.EqualTo(sim.SimulationTime), $"Default simulation time should be {sim.SimulationTime}.");
            Assert.That(presetData.PresetSimData.TimeScale, Is.EqualTo(sim.TimeScale), $"Default time scale should be {sim.TimeScale}.");
            Assert.That(presetData.PresetSimData.IsTimeForward, Is.EqualTo(sim.IsTimeForward), $"Default time direction should be {(sim.IsTimeForward ? "forwards" : "backwards")}.");
            Assert.That(presetData.PresetBodyDataArray, Is.Not.Null, "Body data array should not be null.");
            Assert.That(presetData.PresetBodyDataArray, Has.Length.EqualTo(sim.Bodies.Count), $"Body data array length should be {sim.Bodies.Count} for a new simulation.");
        });
    }

    [Test(Description = "Ensures GetPresetData accurately captures the state of a simulation with multiple bodies.")]
    public void GetPresetData_WithMultipleBodies_ReturnsCorrectPreset()
    {
        // Arrange
        // We create a known preset and load it to establish a specific, known state in the engine.
        var expectedSimData = new PresetSimData(123.45, 1.5, true);
        var expectedBody1 = new PresetBodyData(1, true, 10, 1.1, 1.2, 1.3, 1.4);
        var expectedBody2 = new PresetBodyData(2, false, 20, 2.1, 2.2, 2.3, 2.4);
        var expectedPreset = new PresetData(expectedSimData, [expectedBody1, expectedBody2]);
        physicsEngine.LoadPreset(expectedPreset);

        // Act
        var actualPreset = physicsEngine.GetPresetData();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(actualPreset.PresetSimData, Is.EqualTo(expectedPreset.PresetSimData), "The simulation data in the preset should match the expected state.");
            Assert.That(actualPreset.PresetBodyDataArray, Is.EqualTo(expectedPreset.PresetBodyDataArray), "The of bodies in the simulation data should match the expected state.");
        });
    }


    [Test(Description = "Confirms that loading a preset overwrites the engine's internal state to match the preset data.")]
    public void LoadPreset_WithValidData_CorrectlyUpdatesInternalState()
    {
        // Arrange
        var inputPreset = new PresetData(
            new PresetSimData(50.0, 2.0, false),
            [new PresetBodyData(10, true, 5, 5, 5, 5, 5)]
        );

        // Act
        physicsEngine.LoadPreset(inputPreset);

        // Assert
        // Verify the side effect by getting the state back out and comparing.
        var resultingPreset = physicsEngine.GetPresetData();
        Assert.Multiple(() =>
        {
            Assert.That(resultingPreset.PresetSimData, Is.EqualTo(inputPreset.PresetSimData), "The engine's simulation data did not update correctly.");
            Assert.That(resultingPreset.PresetBodyDataArray, Is.EqualTo(inputPreset.PresetBodyDataArray), "The engine's body data did not update correctly.");
        });
    }

    [Test(Description = "Checks that the TickData returned by LoadPreset accurately reflects the state of the just-loaded preset.")]
    public void LoadPreset_WithValidData_ReturnsCorrectTickData()
    {
        // Arrange
        var inputPreset = new PresetData(
            new PresetSimData(99.9, 0.25, true),
            [
                new PresetBodyData(1, true, 1, 1, 1, 1, 1),
                new PresetBodyData(2, true, 2, 2, 2, 2, 2)
            ]
        );

        // Act
        var returnedTickData = physicsEngine.LoadPreset(inputPreset);

        // Assert
        Assert.Multiple(() =>
        {
            // Check that the returned SimTickData matches the input PresetSimData
            Assert.That(returnedTickData.SimTickData.SimulationTime, Is.EqualTo(inputPreset.PresetSimData.SimulationTime), "Returned TickData has incorrect SimulationTime.");
            Assert.That(returnedTickData.SimTickData.TimeScale, Is.EqualTo(inputPreset.PresetSimData.TimeScale), "Returned TickData has incorrect TimeScale.");
            Assert.That(returnedTickData.SimTickData.IsTimeForward, Is.EqualTo(inputPreset.PresetSimData.IsTimeForward), "Returned TickData has incorrect IsTimeForward.");

            // Check that the returned BodyTickDataArray matches the input PresetBodyDataArray
            Assert.That(returnedTickData.BodyTickDataArray, Has.Length.EqualTo(inputPreset.PresetBodyDataArray.Length), "Returned TickData has an incorrect number of bodies.");

            // Compare each body's data property by property
            for (int i = 0; i < inputPreset.PresetBodyDataArray.Length; i++)
            {
                var expected = inputPreset.PresetBodyDataArray[i];
                var actual = returnedTickData.BodyTickDataArray[i];
                Assert.That(actual.Id, Is.EqualTo(expected.Id), $"Body at index {i} has incorrect Id.");
                Assert.That(actual.Enabled, Is.EqualTo(expected.Enabled), $"Body at index {i} has incorrect Enabled state.");
                Assert.That(actual.Mass, Is.EqualTo(expected.Mass), $"Body at index {i} has incorrect Mass.");
                Assert.That(actual.PosX, Is.EqualTo(expected.PosX), $"Body at index {i} has incorrect PosX.");
                Assert.That(actual.PosY, Is.EqualTo(expected.PosY), $"Body at index {i} has incorrect PosY.");
                Assert.That(actual.VelX, Is.EqualTo(expected.VelX), $"Body at index {i} has incorrect VelX.");
                Assert.That(actual.VelY, Is.EqualTo(expected.VelY), $"Body at index {i} has incorrect VelY.");
            }
        });
    }

    [Test(Description = "Ensures that loading a preset completely replaces a pre-existing simulation state, not merges with it.")]
    public void LoadPreset_OnExistingSimulation_OverwritesPreviousState()
    {
        // Arrange
        // Create an initial state with 5 bodies.
        for (int i = 0; i < 5; i++) physicsEngine.CreateBody();
        var initialBodyCount = physicsEngine.GetPresetData().PresetBodyDataArray.Length;
        Assert.That(initialBodyCount, Is.EqualTo(5), "Precondition failed: Initial simulation should have 5 bodies.");

        // 2. Create a new, different preset with only 2 bodies.

        var b1Preset = CelestialBody.Create(1).GetPresetBodyData();
        var b2Preset = CelestialBody.Create(2).GetPresetBodyData();

        var overwritePreset = new PresetData( new PresetSimData(1000, 1, true), [b1Preset, b2Preset] );

        // Act
        physicsEngine.LoadPreset(overwritePreset);

        // Assert
        var finalPreset = physicsEngine.GetPresetData();
        Assert.That(finalPreset.PresetBodyDataArray, Has.Length.EqualTo(overwritePreset.PresetBodyDataArray.Length), "The number of bodies should be equal to the new preset after loading.");
        Assert.That(finalPreset.PresetBodyDataArray, Is.EqualTo(overwritePreset.PresetBodyDataArray), "The final body data should match the overwrite preset.");
    }

    [Test(Description = "Verifies that loading a preset with no bodies successfully removes all bodies from the simulation.")]
    public void LoadPreset_WithEmptyBodyArray_ClearsAllBodies()
    {
        // Arrange
        // 1. Create an initial state with 5 bodies.
        for (int i = 0; i < 5; i++) physicsEngine.CreateBody();
        Assert.That(physicsEngine.GetPresetData().PresetBodyDataArray, Is.Not.Empty, "Precondition failed: Initial simulation should not be empty.");

        // 2. Create a preset with an empty body array.
        var emptyBodyPreset = new PresetData(new PresetSimData(555, 5, false), []);

        // Act
        physicsEngine.LoadPreset(emptyBodyPreset);

        // Assert
        var finalPreset = physicsEngine.GetPresetData();
        Assert.Multiple(() =>
        {
            Assert.That(finalPreset.PresetBodyDataArray, Is.Empty, "The final body array should be empty.");
            Assert.That(finalPreset.PresetSimData, Is.EqualTo(emptyBodyPreset.PresetSimData), "The simulation data should be updated even when clearing bodies.");
        });
    }

    #endregion

    #region Body Interface Tests
    
    /*  Tests to add

        CreateBody()
        - should return an integer
        - should increase the number of bodies of the simulation
        
        DeleteBody()
        - should return false if no bodies are in the simulation
        - should return false if no body with the given id is in the simulation
        - should return true if a body was deleted
        - should decrease the number of bodies in the simulation if successful
        - should not decrease the number of bodies in the simulation if unsuccessful

        UpdateBody() -> add tests once CelestialBody.Update has been properly implemented

        GetBodyTickData() 
        - should return null if no bodies are in the simulation
        - should return null if no body with the given id is in the simulation
        - should return BodyTickData if the body exists in the simulation
        - returned BodyTickData.Id should equal the given id argument
        - returned BodyTickData should have default values (other than Id) for a body newly added via CreateBody()
    */

    #endregion
}
