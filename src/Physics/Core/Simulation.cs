using Physics.Bodies;

namespace Physics.Core;

internal class Simulation
{
    #region Factory

    static readonly PresetSimData DEFAULT_SIM_DATA = new(0.0, 1.0, true);

    private Simulation(PresetSimData presetSimData, PresetBodyData[] presetBodyDataArray)
    {
        _simulationTime = presetSimData.SimulationTime;
        _timeScale = presetSimData.TimeScale;
        _isTimeForward = presetSimData.IsTimeForward;

        foreach (var bodyPreset in presetBodyDataArray)
        {
            bool added = TryAddBody(bodyPreset);
            if (!added) throw new ArgumentException($"Contains contains more than one body with Id '{bodyPreset.Id}'.", nameof(presetBodyDataArray));
        }
    }

    internal static Simulation Create(PresetData presetData)
        => new(presetData.PresetSimData, presetData.PresetBodyDataArray);

    internal static Simulation Create(PresetSimData presetSimData, PresetBodyData[] presetBodyDataArray)
        => new(presetSimData, presetBodyDataArray);

    internal static Simulation Create()
        => new(DEFAULT_SIM_DATA, []);

    #endregion


    #region Fields & Properties

    double _simulationTime;
    double _timeScale;
    bool _isTimeForward;
    readonly Dictionary<int, CelestialBody> _bodies = [];
    readonly List<CelestialBody> _enabledBodies = [];

    internal double SimulationTime { get => _simulationTime; private set => _simulationTime = value; }
    internal double TimeScale { get => _timeScale; private set => _timeScale = value; }
    internal bool IsTimeForward { get => _isTimeForward; private set => _isTimeForward = value; }
    internal IReadOnlyDictionary<int, CelestialBody> Bodies => _bodies;
    internal IReadOnlyList<CelestialBody> EnabledBodies => _enabledBodies;

    #endregion


    #region DTOs

    internal TickData GetTickData() => new(GetSimTickData(), [.. Bodies.Values.Select(b => b.GetBodyTickData())]);
    internal SimTickData GetSimTickData() => new(SimulationTime, TimeScale, IsTimeForward);

    internal PresetData GetPresetData() => new(GetPresetSimData(), [.. Bodies.Values.Select(b => b.GetPresetBodyData())]);
    internal PresetSimData GetPresetSimData() => new(SimulationTime, TimeScale, IsTimeForward);

    #endregion


    #region Body Management

    int _nextAvailableId = 1;

    internal CelestialBody CreateBody(int maxBodies = 10000)
    {
        if (_bodies.Count >= maxBodies) throw new InvalidOperationException($"Cannot exceed maximum number of bodies: {maxBodies}.");
        while (_bodies.ContainsKey(_nextAvailableId)) _nextAvailableId++;

        int validId = _nextAvailableId;
        _nextAvailableId++;

        var body = CelestialBody.Create(validId);
        AddBody(body);
        return body;
    }

    internal bool TryAddBody(PresetBodyData bodyData)
    {
        if (_bodies.ContainsKey(bodyData.Id)) return false;
        AddBody(CelestialBody.Create(bodyData));
        return true;
    }

    internal bool TryAddBody(CelestialBody body)
    {
        if (_bodies.ContainsKey(body.Id)) return false;
        AddBody(body);
        return true;
    }

    internal bool DeleteBody(int id)
    {
        if (!_bodies.TryGetValue(id, out var body)) return false;

        body.Updated -= OnBodyUpdated;
        if (body.Enabled) _enabledBodies.Remove(body);
        _bodies.Remove(id);
        return true;
    }

    internal CelestialBody? TryGetBody(int id) => _bodies.GetValueOrDefault(id);


    private void OnBodyUpdated(CelestialBody body, BodyDataPartial delta)
    {
        if (delta.Enabled == true && !_enabledBodies.Contains(body))
        {
            _enabledBodies.Add(body);
        }
        else if (delta.Enabled == false) _enabledBodies.Remove(body);
    }

    private void AddBody(CelestialBody body)
    {
        _bodies.Add(body.Id, body);
        if (body.Enabled) _enabledBodies.Add(body);
        body.Updated += OnBodyUpdated;
    }

    #endregion

    internal Simulation Tick(double deltaTime)
    {
        return this;
    }
}
