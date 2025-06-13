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

    internal static readonly MemoryBufferHandler _memoryBufferHandler; 

    private static string testString = "";

    static EngineBridge()
    {
        physicsEngine = new PhysicsEngine();
        _memoryBufferHandler = new MemoryBufferHandler();
    }

    [JSExport]
    public static string[] GetSimStateLayout() => MemoryBufferHandler.SimStateLayout;

    [JSExport]
    public static string[] GetBodyStateLayout() => MemoryBufferHandler.BodyStateLayout;

    [JSExport]
    public static void SetTestString(string newTestString) => testString = newTestString;

    [JSExport]
    public static string GetTestString() => testString;
}

