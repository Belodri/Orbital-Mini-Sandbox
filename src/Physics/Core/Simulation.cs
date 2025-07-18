using Physics.Bodies;

namespace Physics.Core;

internal interface ISimulation
{
    /// <summary>
    /// Gets the timer responsible for managing the flow and scale of time within the simulation.
    /// </summary>
    /// <seealso cref="ITimer"/>
    ITimer Timer { get; }

    /// <summary>
    /// Gets the grid used for spatial partitioning of bodies to optimize force calculations.
    /// </summary>
    /// <seealso cref="IGrid"/>
    IGrid Grid { get; }

    /// <summary>
    /// Gets the calculator that contains the logic for determining physical interactions, such as gravitational forces.
    /// </summary>
    /// <seealso cref="ICalculator"/>
    ICalculator Calculator { get; }

    /// <summary>
    /// Provides read-only access to all celestial bodies currently in the simulation, indexed by their unique ID.
    /// </summary>
    IReadOnlyDictionary<int, ICelestialBody> Bodies { get; }

    /// <summary>
    /// Creates a new celestial body using a factory function, assigns it a unique ID, and adds it to the simulation.
    /// <example><code>
    /// ICelestialBody myPlanet = simulation.CreateBody(id => new Planet(id));
    /// </code></example>
    /// </summary>
    /// <param name="bodyFactory">
    /// A factory function that takes a unique integer ID as input and returns a new instance of an <see cref="ICelestialBody"/> implementation.
    /// The function is responsible for constructing the body and assigning the provided ID.
    /// </param>
    /// <returns>The newly created and added celestial body instance.</returns>
    ICelestialBody CreateBody(Func<int, ICelestialBody> bodyFactory);

    /// <summary>
    /// Attempts to add a pre-existing celestial body to the simulation.
    /// </summary>
    /// <param name="body">The celestial body to add. Its ID must not already exist in the simulation.</param>
    /// <returns><c>true</c> if the body was added successfully; <c>false</c> if a body with the same ID already exists.</returns>
    bool TryAddBody(ICelestialBody body);

    /// <summary>
    /// Attempts to remove a celestial body from the simulation using its ID.
    /// </summary>
    /// <param name="id">The unique ID of the body to remove.</param>
    /// <returns><c>true</c> if a body with the specified ID was found and removed; otherwise <c>false</c>.</returns>
    bool TryDeleteBody(int id);

    /// <summary>
    /// Attempts to remove a specific celestial body instance from the simulation.
    /// </summary>
    /// <param name="body">The celestial body instance to remove.</param>
    /// <returns><c>true</c> if the specified body instance was found and removed; otherwise <c>false</c>.</returns>
    /// <remarks>
    /// This overload is more specific than deleting by ID. It ensures that the exact object reference passed is the
    /// one being removed, which can prevent accidental deletion in complex scenarios involving stale references.
    /// </remarks>
    bool TryDeleteBody(ICelestialBody body);

    /// <summary>
    /// Advances the simulation by a single step.
    /// </summary>
    /// <param name="realDeltaTimeMs">The elapsed real-world time, in milliseconds, since the last tick was called.
    /// This is used to calculate the corresponding simulation time step.</param>
    /// <remarks>
    /// Executing a tick typically involves calculating forces on all enabled bodies, updating their velocities and
    /// positions based on the simulation's time scale, and rebuilding spatial partitioning structures.
    /// </remarks>
    void Tick(double realDeltaTimeMs);
}

internal class Simulation : ISimulation
{
    #region Constructors

    internal Simulation(
        ITimer timer,
        IGrid grid,
        ICalculator calculator,
        IEnumerable<ICelestialBody>? bodies = null)
    {
        Timer = timer;
        Grid = grid;
        Calculator = calculator;

        if (bodies != null)
        {
            foreach (var body in bodies)
            {
                bool added = TryAddBody(body);
                if(!added) throw new ArgumentException($"Contains contains more than one body with Id '{body.Id}'.", nameof(bodies));
            }
        }
    }

    #endregion


    #region Consts & Config

    private const int MAX_BODIES = 1000;

    #endregion


    #region Fields & Properties

    readonly Dictionary<int, ICelestialBody> _bodies = [];
    readonly List<ICelestialBody> _enabledBodies = [];

    /// <inheritdoc/>
    public ITimer Timer { get; private set; }
    /// <inheritdoc/>
    public IGrid Grid { get; private set; }
    /// <inheritdoc/>
    public ICalculator Calculator { get; private set; }
    /// <inheritdoc/>
    public IReadOnlyDictionary<int, ICelestialBody> Bodies => _bodies;

    #endregion


    #region Body Management

    int _nextAvailableId = 0;

    /// <inheritdoc/>
    public ICelestialBody CreateBody(Func<int, ICelestialBody> bodyFactory)
    {
        if (_bodies.Count >= MAX_BODIES) throw new InvalidOperationException($"Cannot exceed maximum number of bodies: {MAX_BODIES}.");

        var id = GetAvailableBodyId();
        ICelestialBody body = bodyFactory(id);
        AddBodyWorker(body);
        return body;
    }

    private int GetAvailableBodyId()
    {
        while (_bodies.ContainsKey(_nextAvailableId)) _nextAvailableId++;
        var id = _nextAvailableId;
        _nextAvailableId++;
        return id;
    }

    /// <inheritdoc/>
    public bool TryAddBody(ICelestialBody body)
    {
        if (_bodies.ContainsKey(body.Id)) return false;
        AddBodyWorker(body);
        return true;
    }

    private void AddBodyWorker(ICelestialBody body)
    {
        _bodies.Add(body.Id, body);
        if (body.Enabled) _enabledBodies.Add(body);
        body.EnabledChanged += OnBodyEnabledToggle;
    }

    /// <inheritdoc/>
    public bool TryDeleteBody(int id)
    {
        if (!_bodies.TryGetValue(id, out var body)) return false;
        DeleteBodyWorker(body);
        return true;
    }

    /// <inheritdoc/>
    public bool TryDeleteBody(ICelestialBody body)
    {
        if (!_bodies.TryGetValue(body.Id, out var existingBody)) return false;
        if (existingBody != body) return false;
        DeleteBodyWorker(body);
        return true;
    }

    private void DeleteBodyWorker(ICelestialBody body)
    {
        body.EnabledChanged -= OnBodyEnabledToggle;
        if (body.Enabled) _enabledBodies.Remove(body);
        _bodies.Remove(body.Id);
    }

    private void OnBodyEnabledToggle(ICelestialBody body)
    {
        if (body.Enabled == true && !_enabledBodies.Contains(body))
        {
            _enabledBodies.Add(body);
        }
        else if (body.Enabled == false) _enabledBodies.Remove(body);
    }

    #endregion


    #region Tick Management

    /// <summary>
    /// A list of DTOs that contain the data used to update the Bodies to the next state.  
    /// </summary>
    private readonly List<(ICelestialBody body, EvaluationResult result)> _tickBodyUpdatesCache = [];

    /// <inheritdoc/>
    public void Tick(double deltaTime)
    {
        // Calculate the simulation time that has passed since the last tick & 
        // update the simulation time by adding delta of this tick.
        // This is safe because only simTimeDelta is used for the rest of the tick calculation.
        double simTimeDelta = Timer.AdvanceSimTime(deltaTime);

        // Rebuild the QuadTree
        Grid.Rebuild(_enabledBodies);

        // If the grid root is null, we cannot update any bodies so we can just return early.
        if (Grid.Root == null) return;

        // Clear the updates cache
        _tickBodyUpdatesCache.Clear();

        // Calculate the body updates to be performed
        foreach (var body in _enabledBodies)
        {
            var result = Calculator.EvaluateBody(body, simTimeDelta, Grid.Root);
            if (result == null) continue;
            _tickBodyUpdatesCache.Add((body, result.Value));
        }

        // Perform the body updates
        foreach (var (body, result) in _tickBodyUpdatesCache) body.Update(
            position: result.Position,
            velocity: result.Velocity,
            acceleration: result.Acceleration
        );
    }

    #endregion
}
