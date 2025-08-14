namespace Physics.Core;

internal interface ITimer
{
    /// <summary>
    /// The current timestamp of the simulation time in units of days (d).
    /// </summary>
    double SimulationTime { get; }
    /// <summary>
    /// The amount of time that passes in a single simulation step. In units of days (d).
    /// A negative timestep makes the simulation step backwards in time.
    /// </summary>
    /// <remarks>
    /// Altering the timestep of a running simulation breaks time-reversability!
    /// </remarks> 
    double TimeStep { get; }
    /// <summary>
    /// An alias for TimeStep
    /// </summary>
    double DeltaTime { get; }
    /// <summary>
    /// Pre-calculated half of <see cref="DeltaTime"/>
    /// </summary>
    double DeltaTimeHalf { get; }
    /// <summary>
    /// Pre-calculated square of <see cref="DeltaTime"/>
    /// </summary>
    double DeltaTimeSquared { get; }
    /// <summary>
    /// Atomically updates one or more properties of the timer.
    /// Unspecified or null parameters will remain unchanged.
    /// </summary>
    /// <param name="timeStep">The new value for the <see cref="TimeStep"/> property.</param>
    void Update(double? timeStep = null);
    /// <summary>
    /// Advances the simulation time by a single step, as determined by <see cref="TimeStep"/>.
    /// </summary>
    void AdvanceSimTime();
}

internal class Timer(double simulationTime = 0.0, double timeStep = 1.0) : ITimer
{
    /// <inheritdoc />
    public double SimulationTime { get; private set; } = simulationTime;

    /// <inheritdoc />
    public double TimeStep
    {
        get;
        private set
        {
            field = value;
            DeltaTimeHalf = field / 2;
            DeltaTimeSquared = field * field;
        }
    } = timeStep;

    /// <inheritdoc />
    public double DeltaTime => TimeStep;

    /// <inheritdoc />
    public double DeltaTimeHalf { get; private set; }

    /// <inheritdoc />
    public double DeltaTimeSquared { get; private set; }

    /// <inheritdoc />
    public void AdvanceSimTime() => SimulationTime += TimeStep;

    /// <inheritdoc />
    public void Update(double? timeStep)
    {
        if (timeStep is double newTimeStep) TimeStep = newTimeStep;
    }
}
