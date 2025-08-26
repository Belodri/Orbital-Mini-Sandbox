using System.Collections.ObjectModel;
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
    /// Event raised after a body has been added to the simulation.
    /// </summary>
    public event Action<ICelestialBody> BodyAdded;
    /// <summary>
    /// Event raised after a body has been removed from the simulation.
    /// </summary>
    public event Action<int> BodyRemoved;
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
    /// Try to add an existing body to the simulation.
    /// </summary>
    /// <param name="body">The body to add.</param>
    /// <returns><c>true</c> if the body was added, <c>false</c> otherwise, meaning the simulation already has a body with the same ID.</returns>
    bool TryAddBody(ICelestialBody body);
    /// <summary>
    /// Removes a celestial body from the simulation using its ID. Does nothing if no body with that ID is found.
    /// </summary>
    /// <param name="id">The ID of the body to remove.</param>
    /// <returns><c>true</c> if a body with the specified ID was found and removed; otherwise <c>false</c>.</returns>
    bool TryDeleteBody(int id);
    /// <summary>
    /// Try to update a celestial body in the simulation.
    /// </summary>
    /// <param name="id">The ID of the body to update.</param>
    /// <param name="updates">Partial data to update the body.</param>
    /// <returns><c>true</c> if the update was successful, <c>false</c> if the body wasn't found.</returns>
    /// <remarks>
    /// This should be the ONLY way to update a body within a simulation!
    /// </remarks>
    public bool TryUpdateBody(int id, BodyDataUpdates updates);
    /// <summary>
    /// Advances the simulation by a single timestep.
    /// Calculates the forces on all enabled bodies and updates their properties like position, velocity and acceleration.
    /// </summary>
    void StepFunction();
}


internal sealed class Simulation(ITimer timer, QuadTree quadTree, ICalculator calculator) : ISimulation
{
    private class BodySet : KeyedCollection<int, ICelestialBody>
    {
        public IReadOnlyList<ICelestialBody> AsList => (IReadOnlyList<ICelestialBody>)Items;
        protected override int GetKeyForItem(ICelestialBody body) => body.Id;
    }


    /// <summary>
    /// Flag to indicate whether a resynchronization of the simulation state 
    /// is necessary before the next time step.
    /// </summary>
    private bool _queueSync = true;
    private int _nextBodyId = 0;
    private readonly Dictionary<int, ICelestialBody> _bodies = [];
    private readonly BodySet _enabledBodies = [];

    public ITimer Timer { get; init; } = timer;
    public QuadTree QuadTree { get; init; } = quadTree;
    public ICalculator Calculator { get; init; } = calculator;
    public IReadOnlyDictionary<int, ICelestialBody> Bodies => _bodies;
    public event Action<ICelestialBody>? BodyAdded;
    public event Action<int>? BodyRemoved;

    public ICelestialBody CreateBody(Func<int, ICelestialBody> bodyFactory)
    {
        while (_bodies.ContainsKey(_nextBodyId)) _nextBodyId++;
        int id = _nextBodyId;
        _nextBodyId++;

        ICelestialBody body = bodyFactory(id);
        if (!TryAddBody(body)) throw new InvalidOperationException($"Failed to add body created by {nameof(bodyFactory)}.");
        return body;
    }

    public bool TryAddBody(ICelestialBody body)
    {
        if (!_bodies.TryAdd(body.Id, body)) return false;

        if (body.Enabled)
        {
            _enabledBodies.Add(body);
            _queueSync = true;
        }
        BodyAdded?.Invoke(body);

        return true;
    }

    public bool TryDeleteBody(int id)
    {
        if (!_bodies.TryGetValue(id, out var body)) return false;

        _bodies.Remove(id);
        if (body.Enabled)
        {
            _enabledBodies.Remove(body);
            _queueSync = true;
        }
        BodyRemoved?.Invoke(id);

        return true;
    }

    public bool TryUpdateBody(int id, BodyDataUpdates updates)
    {
        if (!_bodies.TryGetValue(id, out var body)) return false;

        body.Update(updates);

        var containedInEnabled = _enabledBodies.Contains(id);
        // Queue a resync if the body was in the list of enabled bodies or if its enabled after the update.
        if (containedInEnabled || body.Enabled) _queueSync = true;
        // Then add or remove it from the list of enabled bodies as needed.
        if (body.Enabled && !containedInEnabled) _enabledBodies.Add(body);
        else if(!body.Enabled && containedInEnabled) _enabledBodies.Remove(body.Id);

        return true;
    }

    public void StepFunction()
    {
        VelocityVerletStep();
        Timer.AdvanceSimTime();
    }

    private void VelocityVerletStep()
    {
        if (_enabledBodies.Count == 0) return;

        if (_queueSync)
        {
            // If a resynchronization of the system is necessary
            // (on initial step or after a body has been added/deleted/modified externally),
            // re-evaluate the forces at time t.
            RebuildQuadTree();
            for (int i = 0; i < _enabledBodies.Count; i++)
            {
                var body = _enabledBodies.AsList[i];
                var a = QuadTree.CalcAcceleration(body, Calculator);
                body.Update(acceleration: a);
            }

            _queueSync = false;
        }

        for (int i = 0; i < _enabledBodies.Count; i++)
        {
            var body = _enabledBodies.AsList[i];

            // Step 1: Half-Kick
            // v(t + Δt/2) = v(t) + (a(t)Δt)/2
            var v_half = body.Velocity + body.Acceleration * Timer.DeltaTimeHalf;

            // Step 2: Drift
            // x(t + Δt) = x(t) + v(t + Δt/2)Δt
            var x = body.Position + v_half * Timer.DeltaTime;

            body.Update(position: x, velocityHalfStep: v_half);
        }

        RebuildQuadTree();

        for (int i = 0; i < _enabledBodies.Count; i++)
        {
            var body = _enabledBodies.AsList[i];

            // Step 3: Force
            // a(t+Δt) = F(x(t + Δt))/m
            var a = QuadTree.CalcAcceleration(body, Calculator);

            // Step 4: Half-Kick
            // v(t + Δt) = v(t + Δt/2) + a(t + Δt)Δt/2
            var v = body.VelocityHalfStep + a * Timer.DeltaTimeHalf;

            body.Update(acceleration: a, velocity: v);
        }
    }

    private void RebuildQuadTree()
    {
        double minX = double.MaxValue;
        double minY = double.MaxValue;
        double maxX = double.MinValue;
        double maxY = double.MinValue;

        for (int i = 0; i < _enabledBodies.Count; i++)
        {
            var body = _enabledBodies.AsList[i];
            minX = Math.Min(minX, body.Position.X);
            minY = Math.Min(minY, body.Position.Y);
            maxX = Math.Max(maxX, body.Position.X);
            maxY = Math.Max(maxY, body.Position.Y);
        }
        QuadTree.Reset(minX, minY, maxX, maxY, _enabledBodies.Count);
        for (int i = 0; i < _enabledBodies.Count; i++) QuadTree.InsertBody(_enabledBodies.AsList[i]);
        QuadTree.Evaluate();
    }
}
