using Bridge;
using Physics;
using System.Text.Json;
using System.Reflection;
using NUnit.Framework.Internal;
using static BridgeTests.TestHelpers;

#pragma warning disable CA1416 // Validate platform compatibility

namespace BridgeTests;

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

[TestFixture]
public partial class EngineBridgeTests
{
    #region Helpers

    static readonly SimDataBase simDataBaseA = new(
        SimulationTime: 123.45,
        TimeScale: 1.5,
        IsTimeForward: true,
        TimeConversionFactor: 6000000,
        Theta: 0.7,
        GravitationalConstant: 7.89e12,
        Epsilon: 0.023,
        IntegrationAlgorithm.SymplecticEuler
    );

    static readonly BodyDataBase bodyDataBase1 = new(1, true, 10.0, 1.1, 1.2, 1.3, 1.4, 1.5, 1.6);
    static readonly BodyDataBase bodyDataBase2 = new(2, false, 20.0, 2.1, 2.2, 2.3, 2.4, 2.5, 2.6);

    static readonly PresetData presetWithBodies = new(simDataBaseA, [bodyDataBase1, bodyDataBase2]);
    static readonly PresetData presetWithoutBodies = new(simDataBaseA, []);

    // Note: The expected JSON must be a single line (WriteIndented = false)
    // and have camelCase property names (PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase).
    static readonly string presetWithBodiesJson = "{\"sim\":{\"simulationTime\":123.45,\"timeScale\":1.5,\"isTimeForward\":true,\"timeConversionFactor\":6000000,\"theta\":0.7,\"gravitationalConstant\":7890000000000,\"epsilon\":0.023,\"integrationAlgorithm\":0},\"bodies\":[{\"id\":1,\"enabled\":true,\"mass\":10,\"posX\":1.1,\"posY\":1.2,\"velX\":1.3,\"velY\":1.4,\"accX\":1.5,\"accY\":1.6},{\"id\":2,\"enabled\":false,\"mass\":20,\"posX\":2.1,\"posY\":2.2,\"velX\":2.3,\"velY\":2.4,\"accX\":2.5,\"accY\":2.6}]}";
    static readonly string presetWithoutBodiesJson = "{\"sim\":{\"simulationTime\":123.45,\"timeScale\":1.5,\"isTimeForward\":true,\"timeConversionFactor\":6000000,\"theta\":0.7,\"gravitationalConstant\":7890000000000,\"epsilon\":0.023,\"integrationAlgorithm\":0},\"bodies\":[]}";

    #endregion


    #region Tests for _CreatePresetString

    [Test(Description = "Ensures _CreatePresetString serializes a PresetData object with multiple bodies into the correct compact, camelCase JSON string.")]
    public void CreatePresetString_WithMultipleBodies_ReturnsCorrectJson()
    {

        var presetData = presetWithBodies;
        var expectedJson = presetWithBodiesJson;

        var actualJson = EngineBridge.CreatePresetString(presetData);
        Assert.That(actualJson, Is.EqualTo(expectedJson), "The serialized JSON string did not match the expected format.");
    }

    [Test(Description = "Ensures _CreatePresetString correctly serializes a PresetData object that contains an empty array of bodies.")]
    public void CreatePresetString_WithNoBodies_ReturnsJsonWithEmptyArray()
    {
        var presetData = presetWithoutBodies;
        var expectedJson = presetWithoutBodiesJson;

        var actualJson = EngineBridge.CreatePresetString(presetData);
        Assert.That(actualJson, Is.EqualTo(expectedJson), "The JSON for a preset with no bodies should contain an empty array.");
    }

    #endregion

    #region Tests for _ParseJsonPreset

    [Test(Description = "Ensures _ParseJsonPreset can successfully deserialize a valid JSON string into a correct PresetData object.")]
    public void ParseJsonPreset_WithValidJson_ReturnsCorrectPresetDataObject()
    {
        // Arrange
        var jsonPreset = presetWithBodiesJson;
        var expectedPresetData = presetWithBodies;

        var actualPresetData = EngineBridge.ParseJsonPreset(jsonPreset);

        Assert.That(actualPresetData, Is.Not.Null, "Deserialized data should not be null for valid JSON.");
        Assert.Multiple(() =>
        {
            // Assert the simple record property using its value-based equality
            Assert.That(actualPresetData.Sim, Is.EqualTo(expectedPresetData.Sim), "The Sim property did not match.");

            // Assert the array contents explicitly
            Assert.That(actualPresetData.Bodies, Is.Not.Null, "The Bodies should not be null.");
            Assert.That(actualPresetData.Bodies, Has.Count.EqualTo(expectedPresetData.Bodies.Count), "The list should contain the expected number of elements.");
            Assert.That(actualPresetData.Bodies[0], Is.EqualTo(expectedPresetData.Bodies[0]), "The body data in the array did not match.");
        });
    }

    [Test(Description = "Ensures _ParseJsonPreset throws a JsonException when given a malformed JSON string.")]
    public void ParseJsonPreset_WithMalformedJson_ThrowsJsonException()
    {
        var initialPresetData = presetWithBodies;
        var jsonString = EngineBridge.CreatePresetString(initialPresetData);

        var malformedJson = jsonString + ",";
        Assert.Throws<JsonException>(() => EngineBridge.ParseJsonPreset(malformedJson), "Parsing malformed JSON should throw a JsonException.");
    }

    [Test(Description = "Ensures _ParseJsonPreset throws a JsonException for JSON with mismatched data types.")]
    public void ParseJsonPreset_WithMismatchedTypes_ThrowsJsonException()
    {
        var initialPresetData = presetWithBodies;
        var jsonString = EngineBridge.CreatePresetString(initialPresetData);

        // 'isTimeForward' is a string instead of a boolean.
        string substring = "\"isTimeForward\":true";
        string repacement = "\"isTimeForward\":\"true\"";
        string typeMismatchJson = jsonString.Replace(substring, repacement);
        
        Assert.Throws<JsonException>(() => EngineBridge.ParseJsonPreset(typeMismatchJson), "Parsing JSON with type mismatches should throw a JsonException.");
    }

    [Test(Description = "Ensures _ParseJsonPreset returns null when the input string is the JSON literal 'null'.")]
    public void ParseJsonPreset_WithJsonNullLiteral_ReturnsNull()
    {
        var jsonPreset = "null";
        var result = EngineBridge.ParseJsonPreset(jsonPreset);
        Assert.That(result, Is.Null, "Parsing the JSON 'null' literal should result in a null object.");
    }

    [Test(Description = "Ensures _ParseJsonPreset throws a JsonException when given an empty string, as it's not valid JSON.")]
    public void ParseJsonPreset_WithEmptyString_ThrowsJsonException()
    {
        var emptyString = "";
        Assert.Throws<JsonException>(() => EngineBridge.ParseJsonPreset(emptyString), "Parsing an empty string should throw a JsonException.");
    }

    #endregion

    #region Round Trip Test for _ParseJsonPreset and _CreatePresetString 
    
    [Test(Description = "Verifies that serializing a PresetData object and deserializing it back results in a new object with identical property values.")]
    public void CreateAndParse_RoundTrip_ResultsInEquivalentObject()
    {
        // Arrange
        var initialPresetData = presetWithBodies;

        // Act
        // 1. Serialize the initial object to a JSON string.
        var jsonString = EngineBridge.CreatePresetString(initialPresetData);
        
        // 2. Deserialize that same string back into a new object.
        var finalPresetData = EngineBridge.ParseJsonPreset(jsonString);

        // Assert
        Assert.That(finalPresetData, Is.Not.Null, "The deserialized object should not be null after a round trip.");

        // Use Assert.Multiple to check all properties and report all failures at once.
        Assert.Multiple(() =>
        {
            // This assertion works because Sim is a record with only value-type properties,
            // so its default Equals implementation provides value-based comparison.
            Assert.That(finalPresetData.Sim, Is.EqualTo(initialPresetData.Sim), "The Sim property did not match after the round trip.");
            
            // This assertion works because NUnit's Is.EqualTo constraint intelligently compares the
            // contents of collections element-by-element. It relies on the value-based equality
            // of the BodyDataBase record.
            Assert.That(finalPresetData.Bodies, Is.EqualTo(initialPresetData.Bodies), 
                "The Bodies property did not match after the round trip.");
        });
    }

    #endregion
}
