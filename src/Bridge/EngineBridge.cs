using System.Runtime.InteropServices.JavaScript;
using Physics;

namespace Bridge;

internal class Program
{
    private static void Main(string[] args)
    {
        
    }
}

public static partial class EngineBridge
{
    internal static readonly PhysicsEngine physicsEngine;
    internal static readonly MemoryBufferHandler memoryBufferHandler; 
    private static string testString = "";

    static EngineBridge()
    {
        physicsEngine = new PhysicsEngine();
        memoryBufferHandler = new MemoryBufferHandler(100);
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
    public static string[] GetSimStateLayout() => MemoryBufferHandler.SimStateLayout;

    [JSExport]
    public static string[] GetBodyStateLayout() => MemoryBufferHandler.BodyStateLayout;

    [JSExport]
    public static void SetTestString(string newTestString) => testString = newTestString;

    [JSExport]
    public static string GetTestString() => testString;
}
