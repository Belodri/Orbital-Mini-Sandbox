namespace Physics.Core;

internal class Timer
{
    #region Constructors

    internal Timer() : this(DEFAULT_DATA) { }

    internal Timer(TimerData presetData)
    {
        bool validTimeScale = presetData.TimeScale >= TIME_SCALE_MIN
            && presetData.TimeScale <= TIME_SCALE_MAX;
        if (!validTimeScale) throw new ArgumentException($"TimeScale must be between {TIME_SCALE_MIN} and {TIME_SCALE_MAX} (both inclusive).", nameof(presetData));

        SimulationTime = presetData.SimulationTime;
        TimeScale = presetData.TimeScale;
        IsTimeForward = presetData.IsTimeForward;
        TimeConversionFactor = presetData.TimeConversionFactor;
    }

    #endregion


    #region Consts & Config

    internal static readonly TimerData DEFAULT_DATA = new(0.0, 1.0, true, MS_IN_H);

    internal const double TIME_SCALE_MIN = 0.01;
    internal const double TIME_SCALE_MAX = 100;

    // Time Conversion Helpers
    const int MS_IN_S = 1000;
    const int MS_IN_M = 1000 * 60;
    const int MS_IN_H = 1000 * 60 * 60;
    const int MS_IN_D = 1000 * 60 * 60 * 24;

    #endregion


    #region Fields & Properties

    internal double SimulationTime { get; private set; }

    /// <summary>
    /// An easy to control multiplier for the time that passes each tick. 
    /// </summary>
    internal double TimeScale { get; private set; }

    /// <summary>
    /// How many ms in Simulation time is 1ms in real time (assuming TimeScale is 1)?
    /// </summary>
    internal int TimeConversionFactor { get; private set; }
    internal bool IsTimeForward { get; private set; }

    #endregion

    internal double GetSimDeltaTime(double realDeltaTimeMs)
    {
        return Math.Abs(realDeltaTimeMs)    // IsTimeForward alone controls time direction.
            * TimeConversionFactor
            * TimeScale
            * (IsTimeForward ? 1 : -1);
    }

    internal void UpdateSimTime(double delta) => SimulationTime += delta;
}
