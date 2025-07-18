namespace Physics.Core;

internal interface ITimer
{
    /// <summary>
    /// The current timestamp of the simulation time (in s).
    /// </summary>
    double SimulationTime { get; }

    /// <summary>
    /// A multiplier for the time that passes each tick. 
    /// </summary>
    double TimeScale { get; }

    /// <summary>
    /// Controls the direction of time flow.
    /// </summary>
    bool IsTimeForward { get; }

    /// <summary>
    /// The conversion factor for simulation time (in s) to real time (in s)?
    /// </summary>
    int TimeConversionFactor { get; }

    /// <summary>
    /// Atomically updates one or more properties of the timer.
    /// Unspecified or null parameters will remain unchanged.
    /// </summary>
    /// <param name="timeScale"><see cref="TimeScale"/> property. The value will be clamped to the valid range.</param>
    /// <param name="isTimeForward">T<see cref="IsTimeForward"/> property.</param>
    /// <param name="timeConversionFactor"><see cref="TimeConversionFactor"/> property.</param>
    void Update(
        double? timeScale = null,
        bool? isTimeForward = null,
        int? timeConversionFactor = null
    );

    /// <summary>
    /// Calculates how much the simulation time advances in a given amount of real time
    /// and updates the simulation time accordingly.
    /// </summary>
    /// <param name="realDeltaTimeMs">The real-world time (in ms).</param>
    /// <returns>A delta for how much simulation time advanced (in s).</returns>
    double AdvanceSimTime(double realDeltaTimeMs);
}


internal class Timer : ITimer
{
    #region Constructors

    internal Timer(
        double simulationTime = 0.0,
        double timeScale = 1.0,
        bool isTimeForward = true,
        int timeConversionFactor = MS_IN_H)
    {
        bool validTimeScale = timeScale >= TIME_SCALE_MIN
            && timeScale <= TIME_SCALE_MAX;
        if (!validTimeScale) throw new ArgumentException($"TimeScale must be between {TIME_SCALE_MIN} and {TIME_SCALE_MAX} (both inclusive).", nameof(timeScale));

        SimulationTime = simulationTime;
        TimeScale = timeScale;
        IsTimeForward = isTimeForward;
        TimeConversionFactor = timeConversionFactor;
    }

    #endregion


    #region Consts & Config

    private const double TIME_SCALE_MIN = 0.01;
    private const double TIME_SCALE_MAX = 100;

    #if DEBUG
    internal double Test_TimeScaleMin => TIME_SCALE_MIN;
    internal double Test_TimeScaleMax => TIME_SCALE_MAX;
    #endif

    // Time Conversion Helpers
    internal const int MS_IN_S = 1000;
    internal const int MS_IN_M = 1000 * 60;
    internal const int MS_IN_H = 1000 * 60 * 60;
    internal const int MS_IN_D = 1000 * 60 * 60 * 24;

    #endregion


    #region Fields & Properties
    /// <inheritdoc />
    public double SimulationTime { get; private set; }
    /// <inheritdoc />
    public double TimeScale { get; private set; }
    /// <inheritdoc />
    public int TimeConversionFactor { get; private set; }
    /// <inheritdoc />
    public bool IsTimeForward { get; private set; }

    #endregion

    /// <inheritdoc />
    public double AdvanceSimTime(double realDeltaTimeMs)
    {
        double realDeltaInS = realDeltaTimeMs * MS_IN_S;
        double delta = Math.Abs(realDeltaInS)    // IsTimeForward alone controls time direction.
            * TimeConversionFactor
            * TimeScale
            * (IsTimeForward ? 1 : -1);
        SimulationTime += delta;
        return delta;
    }
    
    /// <inheritdoc />
    public void Update(
        double? timeScale = null,
        bool? isTimeForward = null,
        int? timeConversionFactor = null)
    {
        if (timeScale is double newTimeScale) TimeScale = Math.Clamp(newTimeScale, TIME_SCALE_MIN, TIME_SCALE_MAX);
        if (isTimeForward is bool newTimeDir) IsTimeForward = newTimeDir;
        if (timeConversionFactor is int newTimeConvFact) TimeConversionFactor = newTimeConvFact;
    }
}
