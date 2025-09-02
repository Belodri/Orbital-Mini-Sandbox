using Physics.Bodies;
using Physics.Core;
using Physics.Models;
using Timer = Physics.Core.Timer;

namespace Physics;


public abstract class PhysicsEngineBase
{
    /// <inheritdoc cref="SimulationView"/>
    public abstract SimulationView View { get; private protected set; }
    /// <summary>
    /// Advances the simulation by a single step.
    /// </summary>
    public abstract void Tick();
    /// <summary>
    /// Loads a simulation with provided bodies from the given base data.
    /// </summary>
    /// <param name="sim">The base data for the simulation.</param>
    /// <param name="bodies">The base data for all the bodies.</param>
    public abstract void Import(SimDataBase sim, List<BodyDataBase> bodies);
    /// <summary>
    /// Gets a snapshot of the base data that makes up the current simulation.
    /// Does not contain any derived values.
    /// </summary>
    /// <returns>A tuple containing the base simulation data and the base data of all bodies.</returns>
    public abstract (SimDataBase sim, List<BodyDataBase> bodies) Export();
    /// <summary>
    /// Creates a new celestial body in the simulation.
    /// </summary>
    /// <returns>The unique Id of the created body.</returns>
    public abstract int CreateBody();
    /// <summary>
    /// Deletes a celestial body from the simulation.
    /// </summary>
    /// <param name="id">The unique id of the body to delete.</param>
    /// <returns><c>true</c> if the specified body instance was found and removed; otherwise <c>false</c>.</returns>
    public abstract bool DeleteBody(int id);
    /// <summary>
    /// Atomically updates a celestial body in the simulation.
    /// </summary>
    /// <param name="id">The unique id of the body to update.</param>
    /// <param name="updates">
    /// The new values for properties to be updated.
    /// Unspecified (<c>null</c>) parameters will be ignored and their corresponding properties will remain unchanged.
    /// </param>
    /// <returns><c>true</c> if the update was successful, <c>false</c> if not or if the body wasn't found.</returns>
    /// <remarks>
    /// Updating a running simulation breaks time-reversability!
    /// </remarks>
    public abstract bool UpdateBody(int id, BodyDataUpdates updates);
    /// <summary>
    /// Atomically updates the simulation.
    /// </summary>
    /// <param name="updates">
    /// The new values for properties to be updated.
    /// Unspecified (<c>null</c>) parameters will be ignored and their corresponding properties will remain unchanged.
    /// </param>
    /// <remarks>
    /// Updating a running simulation breaks time-reversability!
    /// </remarks>
    public abstract void UpdateSimulation(SimDataUpdates updates);
}


public sealed class PhysicsEngine : PhysicsEngineBase
{
    #region Fields & Properties

    public PhysicsEngine()
    {
        View = new();
        Simulation = new(
            timer: new Timer(),
            quadTree: new QuadTree(),
            calculator: new Calculator(),
            bodyManager: new BodyManager()
        );
    }

    internal Simulation Simulation
    {
        get;
        private set
        {
            field = value;
            View.SetSimulation(field);
        }
    }

    #endregion


    #region Overrides

    public override SimulationView View { get; private protected set; }

    public override void Tick() => Simulation.StepFunction();

    public override void Import(SimDataBase sim, List<BodyDataBase> bodies)
    {
        Simulation newSimulation = new(
            timer: sim.ToTimer(),
            quadTree: new QuadTree(),
            calculator: sim.ToCalculator(),
            bodyManager: new BodyManager()
        );
        foreach (var bodyData in bodies)
        {
            if (!newSimulation.Bodies.TryAddBody(bodyData.ToCelestialBody()))
                throw new InvalidOperationException("Unable to add bodies with identical IDs to the simulation.");
        }

        Simulation = newSimulation;
    }

    public override (SimDataBase sim, List<BodyDataBase> bodies) Export()
    {
        var sim = Simulation.ToSimDataBase();

        List<BodyDataBase> bodies = new(Simulation.Bodies.BodyCount);
        foreach (var body in Simulation.Bodies.AllBodies.Values) bodies.Add(body.ToBodyDataBase());

        return (sim, bodies);
    }

    public override int CreateBody() => Simulation.Bodies.CreateBody((id) => new CelestialBody(id)).Id;

    public override bool DeleteBody(int id) => Simulation.Bodies.TryDeleteBody(id);

    public override bool UpdateBody(int id, BodyDataUpdates updates) => Simulation.Bodies.TryUpdateBody(id, updates);

    public override void UpdateSimulation(SimDataUpdates updates)
    {
        Simulation.Timer.Update(
            timeStep: updates.TimeStep
        );

        Simulation.Calculator.Update(
            g_SI: updates.G_SI,
            theta: updates.Theta,
            epsilon: updates.Epsilon
        );
    }

    #endregion
}

/// <summary>
/// Provides a live, direct, and read-only view into select properties of the simulation's state.
/// </summary>
/// <remarks>
/// This data is transient, represents the current simulation state, and its values should not be cached.
/// </remarks>
public abstract class SimulationViewBase
{
    /// <inheritdoc cref="Timer.SimulationTime"/>
    public abstract double SimulationTime { get; }
    /// <inheritdoc cref="Timer.TimeStep"/>
    public abstract double TimeStep { get; }
    /// <inheritdoc cref="Calculator.G_SI"/>
    public abstract double G_SI { get; }
    /// <inheritdoc cref="Calculator.Theta"/>
    public abstract double Theta { get; }
    /// <inheritdoc cref="Calculator.Epsilon"/>
    public abstract double Epsilon { get; }
    /// <summary>
    /// Provides a read-only list of views for every celestial body currently in the simulation.
    /// </summary>
    /// <remarks>
    /// Each <c>BodyView</c> in this list acts as a lightweight, live proxy to a body within the simulation.
    /// </remarks>
    public abstract IReadOnlyList<BodyView> Bodies { get; }
}

/// <inheritdoc />
public sealed class SimulationView : SimulationViewBase
{
    #region Internal

    private Simulation? _sim;
    private Simulation Sim => _sim ?? throw new InvalidOperationException("SimulationView is not initialized.");
    private readonly List<BodyView> _bodyViews = new(128);

    internal void SetSimulation(Simulation sim)
    {
        if (_sim != null)   // Reset and clear existing event subscriptions
        {
            _bodyViews.Clear();
            _sim.Bodies.BodyAdded -= AddBodyView;
            _sim.Bodies.BodyRemoved -= DeleteBodyView;
        }

        _sim = sim;

        foreach (var (_, body) in _sim.Bodies.AllBodies) AddBodyView(body);
        _sim.Bodies.BodyAdded += AddBodyView;
        _sim.Bodies.BodyRemoved += DeleteBodyView;
    }

    internal void AddBodyView(ICelestialBody body) => _bodyViews.Add(new(body));
    internal void DeleteBodyView(int id)
    {
        int idx = _bodyViews.FindIndex(bodyView => bodyView.Id == id);
        if (idx != -1) _bodyViews.RemoveAt(idx);
    }

    #endregion


    #region Overrides

    public override double SimulationTime => Sim.Timer.SimulationTime;
    public override double TimeStep => Sim.Timer.TimeStep;
    public override double G_SI => Sim.Calculator.G_SI;
    public override double Theta => Sim.Calculator.Theta;
    public override double Epsilon => Sim.Calculator.Epsilon;
    public override IReadOnlyList<BodyView> Bodies => _bodyViews;

    #endregion
}

/// <summary>
/// Provides a live, direct, and read-only view into select properties of a single celestial body's state.
/// </summary>
/// <remarks>
/// This data is transient, represents the current simulation state, and its values should not be cached.
/// </remarks>
public readonly partial struct BodyView
{
    /// <inheritdoc cref="ICelestialBody.Id"/>
    public readonly partial int Id { get; }
    /// <inheritdoc cref="ICelestialBody.Enabled"/>
    public readonly partial bool Enabled { get; }
    /// <inheritdoc cref="ICelestialBody.Mass"/>
    public readonly partial double Mass { get; }
    /// <inheritdoc cref="ICelestialBody.Position"/>
    public readonly partial Vector2D Position { get; }
    /// <inheritdoc cref="ICelestialBody.Velocity"/>
    public readonly partial Vector2D Velocity { get; }
    /// <inheritdoc cref="ICelestialBody.Acceleration"/>
    public readonly partial Vector2D Acceleration { get; }
    /// <inheritdoc cref="ICelestialBody.OutOfBounds"/>
    public readonly partial bool OutOfBounds { get; }
}

public readonly partial struct BodyView()
{
    internal BodyView(ICelestialBody body) : this()
    {
        _body = body;
    }

    private readonly ICelestialBody? _body;
    private readonly ICelestialBody Body => _body ?? throw new InvalidOperationException("BodyView is not initialized.");

    #region Overrides

    public readonly partial int Id => Body.Id;
    public readonly partial bool Enabled => Body.Enabled;
    public readonly partial double Mass => Body.Mass;
    public readonly partial Vector2D Position => Body.Position;
    public readonly partial Vector2D Velocity => Body.Velocity;
    public readonly partial Vector2D Acceleration => Body.Acceleration;
    public readonly partial bool OutOfBounds => Body.OutOfBounds;

    #endregion
}
