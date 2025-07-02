using Physics.Bodies;

namespace Physics.Core;

internal class Simulation(PresetData presetData)
{
    internal static readonly PresetData DEFAULT_PRESET_DATA = new(new PresetSimData(0.0, 1.0, true), []);

    // Constructor params
    internal double SimulationTime { get; set; } = presetData.PresetSimData.SimulationTime;
    internal double TimeScale { get; set; } = presetData.PresetSimData.TimeScale;
    internal bool IsTimeForward { get; set; } = presetData.PresetSimData.IsTimeForward;
    internal Dictionary<int, CelestialBody> Bodies = presetData.PresetBodyDataArray
        .Select(bd => new CelestialBody(bd))
        .ToDictionary(b => b.Id);

    #region DTOs

    internal TickData GetTickData() => new(GetSimTickData(), [.. Bodies.Values.Select(b => b.GetBodyTickData())]);
    internal SimTickData GetSimTickData() => new(SimulationTime, TimeScale, IsTimeForward);

    internal PresetData GetPresetData() => new(GetPresetSimData(), [.. Bodies.Values.Select(b => b.GetPresetBodyData())]);
    internal PresetSimData GetPresetSimData() => new(SimulationTime, TimeScale, IsTimeForward);

    #endregion


    #region Body Management

    int _nextAvailableId = 1;

    internal CelestialBody AddNewBody(int maxBodies = 10000)
    {
        if (Bodies.Count >= maxBodies) throw new InvalidOperationException($"Cannot exceed maximum number of bodies: {maxBodies}.");
        while (Bodies.ContainsKey(_nextAvailableId)) _nextAvailableId++;

        int validId = _nextAvailableId;
        _nextAvailableId++;

        var bodyData = CelestialBody.DEFAULT_PRESET_DATA with { Id = validId };
        var body = new CelestialBody(bodyData);
        Bodies.Add(body.Id, body);
        return body;
    }

    internal bool DeleteBody(int id) => Bodies.Remove(id);

    internal bool UpdateBody(PresetBodyData updatePreset)
    {
        Bodies.TryGetValue(updatePreset.Id, out var body);
        return body != null && body.Update(updatePreset);
    }

    internal BodyTickData? GetBodyTickData(int id) => Bodies.TryGetValue(id, out var body) ? body.GetBodyTickData() : null;

    #endregion

    internal Simulation Tick(double deltaTime)
    {
        return this;
    }
}
