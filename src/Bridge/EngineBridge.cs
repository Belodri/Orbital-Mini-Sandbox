using System.Runtime.InteropServices.JavaScript;
using Physics;

namespace Bridge;

internal class Program { private static void Main(string[] args) {} }

public static partial class EngineBridge
{

    private static readonly PhysicsEngine physicsEngine;
    private static readonly MemoryBufferHandler memoryBufferHandler;
    private static string testString = "";

    static EngineBridge()
    {
        physicsEngine = new PhysicsEngine();
        memoryBufferHandler = new MemoryBufferHandler();
    }

    [JSExport]
    public static string? Tick(double timestamp)
    {
        try
        {
            TickDataDto tickData = physicsEngine.Tick(timestamp);
            memoryBufferHandler.WriteTickData(tickData);
            return null;
        }
        catch (Exception e)
        {
            return e.Message;
        }
    }

    [JSExport]
    public static void CreateTestSim(int bodyCount)
    {
        physicsEngine.CreateTestSim(bodyCount);
    }

    /// <summary>
    /// Exports the memory address (pointer) of the primary simulation state buffer in the WASM memory heap.
    /// </summary>
    /// <returns>A 32-bit integer representing the byte offset in the WASM heap.</returns>
    [JSExport]
    public static int GetSimStateBufferPtr() => (int)memoryBufferHandler.SimStateBufferPtr;

    /// <summary>
    /// Exports the total size in bytes of the primary simulation state buffer.
    /// </summary>
    [JSExport]
    public static int GetSimStateBufferSize() => memoryBufferHandler.SimStateBufferSizeInBytes;

    [JSExport]
    public static string[] GetSimStateLayout() => MemoryBufferHandler.SimStateLayoutArr;

    [JSExport]
    public static string[] GetBodyStateLayout() => MemoryBufferHandler.BodyStateLayoutArr;

    [JSExport]
    public static void SetTestString(string newTestString) => testString = newTestString;

    [JSExport]
    public static string GetTestString() => testString;
}
