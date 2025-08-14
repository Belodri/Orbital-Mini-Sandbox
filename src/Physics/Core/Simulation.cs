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
    /// Gets the QuadTree used for spatial partitioning of bodies.
    /// </summary>
    /// <seealso cref="QuadTree"/>
    QuadTree QuadTree { get; }

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
    /// Advances the simulation by a single timestep.
    /// Calculates the forces on all enabled bodies and updates their properties like position, velocity and acceleration.
    /// </summary>
    void Tick();
}

internal class Simulation : ISimulation
{
    #region Constructors

    internal Simulation(
        ITimer timer,
        QuadTree quadTree,
        ICalculator calculator,
        IEnumerable<ICelestialBody>? bodies = null)
    {
        Timer = timer;
        QuadTree = quadTree;
        Calculator = calculator;

        if (bodies != null)
        {
            foreach (var body in bodies)
            {
                bool added = TryAddBody(body);
                if (!added) throw new ArgumentException($"Contains contains more than one body with Id '{body.Id}'.", nameof(bodies));
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
    public QuadTree QuadTree { get; private set; }
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

    // Step function using Velocity-Verlet
    // 
    // Separating read/write steps would require either a different integration algorithm
    // (i.e. Leapfrog-Verlet), or a rewrite of QuadTree to accept DTOs instead of actual bodies.
    // Leapfrog is not ideal because we want the calculated 
    // properties x, v, and a to be in sync with one another.

    /// <inheritdoc/>
    public void Tick()
    {
        int bodyCount = _enabledBodies.Count;

        if (bodyCount == 0)
        {
            Timer.AdvanceSimTime();
            return;
        }

        double minX = 0;
        double minY = 0;
        double maxX = 0;
        double maxY = 0;

        for (int i = 0; i < bodyCount; i++)
        {
            var body = _enabledBodies[i];

            // Step 1: Half-Kick
            // v(t + Δt/2) = v(t) + (a(t)Δt)/2
            var v_half = body.Velocity + body.Acceleration * Timer.DeltaTimeHalf;

            // Step 2: Drift
            // x(t + Δt) = x(t) + v(t + Δt/2)Δt
            var x = body.Position + v_half * Timer.DeltaTime;

            // Update body directly
            body.Update(position: x, velocityHalfStep: v_half);

            // Get boundaries for quad-tree
            minX = Math.Min(minX, x.X);
            minY = Math.Min(minY, x.Y);
            maxX = Math.Max(maxX, x.X);
            maxY = Math.Max(maxY, x.Y);
        }

        // Rebuild and evaluate the tree with the new body positions.
        QuadTree.Reset(minX, minY, maxX, maxY, _enabledBodies.Count);
        for (int i = 0; i < bodyCount; i++) QuadTree.InsertBody(_enabledBodies[i]);
        QuadTree.Evaluate();

        for (int i = 0; i < bodyCount; i++)
        {
            var body = _enabledBodies[i];

            // Step 3: Force
            // a(t+Δt) = F(x(t + Δt))/m
            var a = QuadTree.CalcAcceleration(body, Calculator);

            // Step 4: Half-Kick
            // v(t + Δt) = v(t + Δt/2) + a(t + Δt)Δt/2
            var v = body.VelocityHalfStep + a * Timer.DeltaTimeHalf;

            body.Update(acceleration: a, velocity: v);
        }

        Timer.AdvanceSimTime();
    }

    #endregion
}
