using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using System.Runtime.InteropServices.JavaScript;
using Microsoft.JSInterop;
using Physics; 

namespace Bridge;

public partial class Program
{
    public static async Task Main(string[] args)
    {
        // This sets up the basic Blazor WebAssembly host environment.
        // It's required to get the .NET runtime and JS interop working in the browser.
        var builder = WebAssemblyHostBuilder.CreateDefault(args);
        await builder.Build().RunAsync();
    }
}

public struct EngineResult<T>
{
    public bool Success { get; set; }
    public string ErrorMessage { get; set; }
    public T Value { get; set; }
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

    [JSInvokable]
    public static string GetTickErrorText() => lastTickError;
}