using Physics.Bodies;
using Physics.Core;
using Timer = Physics.Core.Timer;

namespace Physics;

public interface IPhysicsEngine
{
    /// <summary>
    /// Advances the simulation by a single step.
    /// </summary>
    /// <param name="realDeltaTimeMs">The elapsed real-world time, in milliseconds, since the last tick was called.</param>
    /// <remarks>
    void Tick(double realDeltaTimeMs);

    /// <summary>
    /// Loads a simulation with provided bodies from the given base data.
    /// </summary>
    /// <param name="sim">The base data for the simulation.</param>
    /// <param name="bodies">The base data for all the bodies.</param>
    void Load(SimDataBase sim, List<BodyDataBase> bodies);

    /// <summary>
    /// Gets a snapshot of the base data that makes up the current simulation.
    /// Does not contain any derived values.
    /// </summary>
    /// <returns>A tuple containing the base simulation data and the base data of all bodies.</returns>
    (SimDataBase sim, List<BodyDataBase> bodies) GetBaseData();

    /// <summary>
    /// Gets a snapshot of the full data of the current simulation.
    /// Contains both the base data as well as derived values.
    /// </summary>
    /// <returns>A tuple containing the full simulation data and the full data of all bodies.</returns>
    (SimDataFull sim, List<BodyDataFull> bodies) GetFullData();

    /// <summary>
    /// Creates a new celestial body in the simulation.
    /// </summary>
    /// <returns>The unique Id of the created body.</returns>
    int CreateBody();

    /// <summary>
    /// Deletes a celestial body from the simulation.
    /// </summary>
    /// <param name="id">The unique id of the body to delete.</param>
    /// <returns><c>true</c> if the specified body instance was found and removed; otherwise <c>false</c>.</returns>
    bool DeleteBody(int id);

    /// <summary>
    /// Atomically updates a celestial body in the simulation.
    /// </summary>
    /// <param name="id">The unique id of the body to update.</param>
    /// <param name="updates">
    /// The new values for properties to be updated.
    /// Unspecified (<c>null</c>) parameters will be ignored and their corresponding properties will remain unchanged.
    /// </param>
    /// <returns><c>true</c> if the update was successful, <c>false</c> if not or if the body wasn't found.</returns>
    bool UpdateBody(int id, BodyDataUpdates updates);

    /// <summary>
    /// Atomically updates the simulation.
    /// </summary>
    /// <param name="updates">
    /// The new values for properties to be updated.
    /// Unspecified (<c>null</c>) parameters will be ignored and their corresponding properties will remain unchanged.
    /// </param>
    void UpdateSimulation(SimDataUpdates updates);
}

public class PhysicsEngine : IPhysicsEngine
{
    #region Fields & Properties

    private Simulation simulation = new(
        timer: new Timer(),
        grid: new Grid(),
        calculator: new Calculator(),
        bodies: null
    );

    #endregion


    #region Public Methods

    /// <inheritdoc/>
    public void Tick(double realDeltaTimeMs) => simulation.Tick(realDeltaTimeMs);

    /// <inheritdoc/>
    public void Load(SimDataBase sim, List<BodyDataBase> bodies)
    {
        List<CelestialBody> bodiesList = [];
        foreach (var bodyData in bodies) bodiesList.Add(bodyData.ToCelestialBody());

        Simulation newSimulation = new(
            timer: sim.ToTimer(),
            grid: sim.ToGrid(),
            calculator: sim.ToCalculator(),
            bodies: bodiesList
        );

        simulation = newSimulation;
    }

    /// <inheritdoc/>
    public (SimDataBase sim, List<BodyDataBase> bodies) GetBaseData()
    {
        var sim = simulation.ToSimDataBase();

        List<BodyDataBase> bodies = new(simulation.Bodies.Count);
        foreach (var body in simulation.Bodies.Values) bodies.Add(body.ToBodyDataBase());

        return (sim, bodies);
    }

    /// <inheritdoc/>
    public (SimDataFull sim, List<BodyDataFull> bodies) GetFullData()
    {
        var sim = simulation.ToSimDataFull();

        List<BodyDataFull> bodies = new(simulation.Bodies.Count);
        foreach (var body in simulation.Bodies.Values) bodies.Add(body.ToBodyDataFull());

        return (sim, bodies);
    }

    /// <inheritdoc/>
    public int CreateBody() => simulation.CreateBody((id) => new CelestialBody(id)).Id;

    /// <inheritdoc/>
    public bool DeleteBody(int id) => simulation.TryDeleteBody(id);

    /// <inheritdoc/>
    public bool UpdateBody(int id, BodyDataUpdates updates)
    {
        if (!simulation.Bodies.TryGetValue(id, out var body)) return false;

        body.Update(
            enabled: updates.Enabled,
            mass: updates.Mass,
            posX: updates.PosX,
            posY: updates.PosY,
            velX: updates.VelX,
            velY: updates.VelY
        );

        return true;
    }

    /// <inheritdoc/>
    public void UpdateSimulation(SimDataUpdates updates)
    {
        simulation.Timer.Update(
            timeScale: updates.TimeScale,
            isTimeForward: updates.IsTimeForward,
            timeConversionFactor: updates.TimeConversionFactor
        );

        simulation.Calculator.Update(
            gravitationalConstant: updates.GravitationalConstant,
            theta: updates.Theta,
            epsilon: updates.Epsilon
        );
    }

    #endregion
}
