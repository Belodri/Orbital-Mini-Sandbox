using System.Runtime.InteropServices.JavaScript;
using Physics;

namespace Bridge;


internal class Program
{
    private static async Task Main(string[] args)
    {
        
    }
}

public static partial class EngineBridge
{
    internal static readonly PhysicsEngine physicsEngine;

    private static string lastTickError = "";

    static EngineBridge()
    {
        physicsEngine = new PhysicsEngine();
    }

    [JSExport]
    public static void Tick()
    {
        physicsEngine.Tick();
        if (lastTickError == "") lastTickError = "TESTING";
        else lastTickError = "";
    }

    [JSExport]
    public static string GetTickErrorText()
    {
        return lastTickError;
    }
}

