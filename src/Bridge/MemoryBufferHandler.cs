using System.Runtime.InteropServices;

namespace Bridge;

internal class MemoryBufferHandler
{
    /// <summary>
    /// Layout for simulation metadata (fixed-size Float64 buffer).
    /// Array index corresponds to field position.
    /// Stride = Array length.
    /// </summary>
    public static readonly string[] SimStateLayout = [
        "tickError",
        "simulationTime",
        "timeScale",
        "timeIsForward",
        "bodyCount"
    ];

    /// <summary>
    /// Layout for individual body state (dynamic Float64 buffer).
    /// Array index corresponds to field position.
    /// Stride = Array length.
    /// </summary>
    public static readonly string[] BodyStateLayout = [
        "id",
        "enabled",
        "mass",
        "posX",
        "posY",
        "velX",
        "velY"
    ];
}