using Physics.Bodies;

namespace Physics.Core;

internal class Simulation
{
    #region Constructors

    internal Simulation() : this(DEFAULT_DATA) { }

    internal Simulation(SimulationData data)
    {
        Timer = new(data.TimerData);

        foreach (var bodyData in data.BodyDataArray)
        {
            bool added = TryAddBody(bodyData);
            if (!added) throw new ArgumentException($"Contains contains more than one body with Id '{bodyData.Id}'.", nameof(data));
        }
    }

    #endregion


    #region Consts & Config

    internal static readonly SimulationData DEFAULT_DATA = new(Timer.DEFAULT_DATA, []);

    #endregion


    #region Fields & Properties

    readonly Dictionary<int, CelestialBody> _bodies = [];
    readonly List<CelestialBody> _enabledBodies = [];

    internal readonly Timer Timer;
    internal readonly Grid Grid = new();

    internal IReadOnlyDictionary<int, CelestialBody> Bodies => _bodies;
    internal IReadOnlyList<CelestialBody> EnabledBodies => _enabledBodies;

    #endregion


    #region Body Management

    int _nextAvailableId = 1;

    internal CelestialBody CreateBody(int maxBodies = 10000)
    {
        if (_bodies.Count >= maxBodies) throw new InvalidOperationException($"Cannot exceed maximum number of bodies: {maxBodies}.");

        while (_bodies.ContainsKey(_nextAvailableId)) _nextAvailableId++;
        var body = AddBody(new(_nextAvailableId));
        _nextAvailableId++;
        return body;
    }

    internal bool TryAddBody(BodyData bodyData)
    {
        if (_bodies.ContainsKey(bodyData.Id)) return false;
        AddBody(new(bodyData));
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

    private CelestialBody AddBody(CelestialBody body)
    {
        _bodies.Add(body.Id, body);
        if (body.Enabled) _enabledBodies.Add(body);
        body.Updated += OnBodyUpdated;
        return body;
    }

    #endregion

    // WORK IN PROGRESS
    internal Simulation Tick(double deltaTime)
    {
        // Calculate the simulation time that has passed since the last tick.
        double simTimeDelta = Timer.GetSimDeltaTime(deltaTime);

        // Rebuild the QuadTree
        Grid.Rebuild(EnabledBodies);

        /*  NEXT STEPS / NOTES

            Implement some kind of dual buffer for bodies to avoid race conditions during calculations.
            readonly Dictionary<int, CelestialBody> _bodiesReadBuffer = [];
            readonly Dictionary<int, CelestialBody> _bodiesWriteBuffer = [];

            Alternatively, consider not updating the bodies directly during calculations but rather storing 
            a BodyDataPartial DTO with the new values and updating all bodies in one go after all calculations 
            are completed.
            This should avoid the need for a dual buffer alltogether!

            - rebuild grid from currentBodies
            
        */

        // Update the simulation time by adding delta of this tick.
        Timer.UpdateSimTime(simTimeDelta);

        return this;
    }
}
