using Bridge;
using Physics;
using System.Text.Json;

#pragma warning disable CA1416 // Validate platform compatibility

namespace BridgeTests;

[TestFixture]
public class EngineBridgeTests
{
    #region Tests for _CreatePresetString

    [Test(Description = "Ensures _CreatePresetString serializes a PresetData object with multiple bodies into the correct compact, camelCase JSON string.")]
    public void CreatePresetString_WithMultipleBodies_ReturnsCorrectJson()
    {
        var simData = new PresetSimData(123.45, 1.5, true);
        var body1 = new PresetBodyData(1, true, 10.0, 1.1, 1.2, 1.3, 1.4);
        var body2 = new PresetBodyData(2, false, 20.0, 2.1, 2.2, 2.3, 2.4);
        var presetData = new PresetData(simData, [body1, body2]);
        // Note: The expected JSON must be a single line (WriteIndented = false)
        // and have camelCase property names (PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase).
        var expectedJson = "{\"presetSimData\":{\"simulationTime\":123.45,\"timeScale\":1.5,\"isTimeForward\":true},\"presetBodyDataArray\":[{\"id\":1,\"enabled\":true,\"mass\":10,\"posX\":1.1,\"posY\":1.2,\"velX\":1.3,\"velY\":1.4},{\"id\":2,\"enabled\":false,\"mass\":20,\"posX\":2.1,\"posY\":2.2,\"velX\":2.3,\"velY\":2.4}]}";

        var actualJson = EngineBridge._CreatePresetString(presetData);
        Assert.That(actualJson, Is.EqualTo(expectedJson), "The serialized JSON string did not match the expected format.");
    }

    [Test(Description = "Ensures _CreatePresetString correctly serializes a PresetData object that contains an empty array of bodies.")]
    public void CreatePresetString_WithNoBodies_ReturnsJsonWithEmptyArray()
    {
        var simData = new PresetSimData(50.0, 1.0, false);
        var presetData = new PresetData(simData, []);
        var expectedJson = "{\"presetSimData\":{\"simulationTime\":50,\"timeScale\":1,\"isTimeForward\":false},\"presetBodyDataArray\":[]}";

        var actualJson = EngineBridge._CreatePresetString(presetData);
        Assert.That(actualJson, Is.EqualTo(expectedJson), "The JSON for a preset with no bodies should contain an empty array.");
    }

    #endregion

    #region Tests for _ParseJsonPreset

    [Test(Description = "Ensures _ParseJsonPreset can successfully deserialize a valid JSON string into a correct PresetData object.")]
    public void ParseJsonPreset_WithValidJson_ReturnsCorrectPresetDataObject()
    {
        // Arrange
        var jsonPreset = "{\"presetSimData\":{\"simulationTime\":99.9,\"timeScale\":2,\"isTimeForward\":false},\"presetBodyDataArray\":[{\"id\":101,\"enabled\":true,\"mass\":50,\"posX\":-10,\"posY\":10,\"velX\":-5,\"velY\":5}]}";

        var expectedSimData = new PresetSimData(99.9, 2.0, false);
        var expectedBody = new PresetBodyData(101, true, 50.0, -10.0, 10.0, -5.0, 5.0);
        // We don't need the full expectedPresetData object for this approach.

        var actualPresetData = EngineBridge._ParseJsonPreset(jsonPreset);

        Assert.That(actualPresetData, Is.Not.Null, "Deserialized data should not be null for valid JSON.");
        Assert.Multiple(() =>
        {
            // Assert the simple record property using its value-based equality
            Assert.That(actualPresetData.PresetSimData, Is.EqualTo(expectedSimData), "The PresetSimData property did not match.");

            // Assert the array contents explicitly
            Assert.That(actualPresetData.PresetBodyDataArray, Is.Not.Null, "The PresetBodyDataArray should not be null.");
            Assert.That(actualPresetData.PresetBodyDataArray, Has.Length.EqualTo(1), "The array should contain exactly one element.");
            Assert.That(actualPresetData.PresetBodyDataArray[0], Is.EqualTo(expectedBody), "The body data in the array did not match.");
        });
    }

    [Test(Description = "Ensures _ParseJsonPreset throws a JsonException when given a malformed JSON string.")]
    public void ParseJsonPreset_WithMalformedJson_ThrowsJsonException()
    {
        // This JSON has a trailing comma after the last property.
        var malformedJson = "{\"presetSimData\":{\"simulationTime\":99.9,},\"presetBodyDataArray\":[]}";
        Assert.Throws<JsonException>(() => EngineBridge._ParseJsonPreset(malformedJson), "Parsing malformed JSON should throw a JsonException.");
    }

    [Test(Description = "Ensures _ParseJsonPreset throws a JsonException for JSON with mismatched data types.")]
    public void ParseJsonPreset_WithMismatchedTypes_ThrowsJsonException()
    {
        // 'mass' is a string instead of a number.
        var typeMismatchJson = "{\"presetSimData\":{\"simulationTime\":99.9,\"timeScale\":2,\"isTimeForward\":false},\"presetBodyDataArray\":[{\"id\":101,\"enabled\":true,\"mass\":\"fifty\",\"posX\":-10,\"posY\":10,\"velX\":-5,\"velY\":5}]}";
        Assert.Throws<JsonException>(() => EngineBridge._ParseJsonPreset(typeMismatchJson), "Parsing JSON with type mismatches should throw a JsonException.");
    }

    [Test(Description = "Ensures _ParseJsonPreset returns null when the input string is the JSON literal 'null'.")]
    public void ParseJsonPreset_WithJsonNullLiteral_ReturnsNull()
    {
        var jsonPreset = "null";
        var result = EngineBridge._ParseJsonPreset(jsonPreset);
        Assert.That(result, Is.Null, "Parsing the JSON 'null' literal should result in a null object.");
    }

    [Test(Description = "Ensures _ParseJsonPreset throws a JsonException when given an empty string, as it's not valid JSON.")]
    public void ParseJsonPreset_WithEmptyString_ThrowsJsonException()
    {
        var emptyString = "";
        Assert.Throws<JsonException>(() => EngineBridge._ParseJsonPreset(emptyString), "Parsing an empty string should throw a JsonException.");
    }

    #endregion

    #region Round Trip Test for _ParseJsonPreset and _CreatePresetString 
    
    [Test(Description = "Verifies that serializing a PresetData object and deserializing it back results in a new object with identical property values.")]
    public void CreateAndParse_RoundTrip_ResultsInEquivalentObject()
    {
        // Arrange
        // Create a reasonably complex object to ensure all properties are handled.
        var initialSimData = new PresetSimData(42.42, 0.5, false);
        var initialBody1 = new PresetBodyData(1, true, 10.0, 100.5, -50.2, 5.5, -2.1);
        var initialBody2 = new PresetBodyData(99, false, 999.9, 0, 0, 0, 0);
        var initialPresetData = new PresetData(initialSimData, [initialBody1, initialBody2]);

        // Act
        // 1. Serialize the initial object to a JSON string.
        var jsonString = EngineBridge._CreatePresetString(initialPresetData);
        
        // 2. Deserialize that same string back into a new object.
        var finalPresetData = EngineBridge._ParseJsonPreset(jsonString);

        // Assert
        Assert.That(finalPresetData, Is.Not.Null, "The deserialized object should not be null after a round trip.");

        // Use Assert.Multiple to check all properties and report all failures at once.
        Assert.Multiple(() =>
        {
            // This assertion works because PresetSimData is a record with only value-type properties,
            // so its default Equals implementation provides value-based comparison.
            Assert.That(finalPresetData.PresetSimData, Is.EqualTo(initialPresetData.PresetSimData), 
                "The PresetSimData property did not match after the round trip.");
            
            // This assertion works because NUnit's Is.EqualTo constraint intelligently compares the
            // contents of collections element-by-element. It relies on the value-based equality
            // of the PresetBodyData record.
            Assert.That(finalPresetData.PresetBodyDataArray, Is.EqualTo(initialPresetData.PresetBodyDataArray), 
                "The PresetBodyDataArray property did not match after the round trip.");
        });
    }

    #endregion
}
