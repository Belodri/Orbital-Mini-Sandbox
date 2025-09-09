using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.JavaScript;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Physics;

namespace Bridge;

internal class Program { private static void Main(string[] args) { } }  // Called while initializing dotnet.js; Don't remove!

internal record PresetData(SimDataBase Sim, List<BodyDataBase> Bodies);

[JsonSourceGenerationOptions(
    WriteIndented = false,
    GenerationMode = JsonSourceGenerationMode.Metadata,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(PresetData))]
[JsonSerializable(typeof(SimDataBase))]
[JsonSerializable(typeof(BodyDataBase))]
[JsonSerializable(typeof(List<BodyDataBase>))]
partial class PresetJSONSerializerContext : JsonSerializerContext { }

public static partial class EngineBridge
{
#if DEBUG
    internal static PhysicsEngine physicsEngine;
#else
    private static PhysicsEngine physicsEngine;
#endif

    private static readonly MemoryBufferHandler memoryBufferHandler;
    private static readonly CommandQueue commandQueue;

    static EngineBridge()
    {
        physicsEngine = new PhysicsEngine();
        memoryBufferHandler = new MemoryBufferHandler();
        commandQueue = new CommandQueue();
    }

    #region Bridge-internal methods

    [JSExport]
    public static string[] GetSimStateLayout() => MemoryBufferHandler.SimStateLayoutArr;

    [JSExport]
    public static string[] GetBodyStateLayout() => MemoryBufferHandler.BodyStateLayoutArr;

    [JSExport]
    public static int[] GetSimBufferPtrAndSize()
    {
        return [
            (int)memoryBufferHandler.SimBufferPtr,
            memoryBufferHandler.SimBufferSizeInBytes,
        ];
    }

    #endregion

    #region Publicly exposed methods

    [JSExport]
    public static void Tick()
    {
#if DEBUG   // High-frequency calls are only logged in debug builds
        Logger.Entry().Log("Start Execution...");
#endif
        // Process queued commands
        commandQueue.ProcessAll(physicsEngine);
        // Let the engine do its calculations
        physicsEngine.Tick();
        // Write the resulting state into the shared memory
        memoryBufferHandler.WriteViewToMemory(physicsEngine.View);
        // Resolve the queued commands
        commandQueue.ResolveProcessed();
#if DEBUG
        Logger.Entry().Log("Finish");
#endif
    }

    [JSExport]
    public static void ProcessQueueNoTick()
    {
#if DEBUG   // High-frequency calls are only logged in debug builds
        Logger.Entry().Log("Start Execution...");
#endif
        commandQueue.ProcessAll(physicsEngine);
        memoryBufferHandler.WriteViewToMemory(physicsEngine.View);
        commandQueue.ResolveProcessed();
#if DEBUG
        Logger.Entry().Log("Finish");
#endif
    }

    [JSExport]
    public static Task<int> CreateBody()
    {
        Logger.Entry().Log("Task Queued");
        return commandQueue.EnqueueTask(engine =>
        {
            var id = engine.CreateBody();
            Logger.Entry(nameof(CreateBody)).WithArg(id).Log("Task Executed");
            return id;
        });
    }

    [JSExport]
    public static Task<bool> DeleteBody(int id)
    {
        Logger.Entry().WithArg(id).Log("Task Queued");

        return commandQueue.EnqueueTask(engine =>
        {
            var wasDeleted = engine.DeleteBody(id);
            Logger.Entry(nameof(DeleteBody)).WithArg(id).WithArg(wasDeleted).Log("Task Executed");
            return wasDeleted;
        });
    }

    [JSExport]
    public static Task<bool> UpdateBody(
        int id,
        bool? enabled = null, double? mass = null,
        double? posX = null, double? posY = null,
        double? velX = null, double? velY = null)
    {
        Logger.Entry()
            .WithArg(id)
            .WithArg(enabled)
            .WithArg(mass)
            .WithArg(posX)
            .WithArg(posY)
            .WithArg(velX)
            .WithArg(velY)
            .Log("Task Queued");

        return commandQueue.EnqueueTask(engine =>
        {
            var wasUpdated = engine.UpdateBody(
                id, new(enabled, mass, posX, posY, velX, velY)
            );
            Logger.Entry(nameof(UpdateBody))
                .WithArg(id).WithArg(wasUpdated)
                .Log("Task Executed");
            return wasUpdated;
        });
    }

    [JSExport]
    public static Task UpdateSimulation(
        double? timeStep = null,
        double? theta = null,
        double? g_SI = null,
        double? epsilon = null)
    {
        Logger.Entry()
            .WithArg(timeStep)
            .WithArg(theta)
            .WithArg(g_SI)
            .WithArg(epsilon)
            .Log("Task Queued");

        return commandQueue.EnqueueTask(engine =>
        {
            engine.UpdateSimulation(
                new(timeStep, theta, g_SI, epsilon)
            );
            Logger.Entry(nameof(UpdateSimulation))
                .WithArg(timeStep)
                .WithArg(theta)
                .WithArg(g_SI)
                .WithArg(epsilon)
                .Log("Task Executed");
        });
    }

    /// <summary>
    /// Serializes the current state of the physics simulation into a JSON string.
    /// </summary>
    /// <returns>
    /// A JSON formatted string representing the current <see cref="PresetData"/>. 
    /// This string can be saved and later loaded using the <see cref="LoadPreset"/> method.
    /// </returns>
    [JSExport]
    public static string GetPreset()
    {
        (SimDataBase sim, List<BodyDataBase> bodies) = physicsEngine.Export();
        PresetData data = new(sim, bodies);
        var jsonPreset = CreatePresetString(data);
        Logger.Entry().WithArg(jsonPreset.Length).Log("Export Preset");
        return jsonPreset;
    }

    internal static string CreatePresetString(PresetData presetData)
    {
        return JsonSerializer.Serialize(presetData, PresetJSONSerializerContext.Default.PresetData);
    }


    /// <summary>
    /// Deserializes a JSON string representing a simulation preset and applies it to the physics engine,
    /// overwriting the current simulation state.
    /// </summary>
    /// <param name="jsonPreset">A string containing the simulation state in JSON format generated by the <see cref="GetPreset"/> method.</param>
    [JSExport]
    public static void LoadPreset(string jsonPreset)
    {
        Logger.Entry().WithArg(jsonPreset.Length).Log("Importing Preset...");

        commandQueue.ClearQueue(); // Ensure prior commands cannot interfere with the newly loaded state.
        PresetData? data = ParseJsonPreset(jsonPreset) ?? throw new ArgumentException("Failed to load: Preset data was null or empty.", nameof(jsonPreset));
        physicsEngine.Import(data.Sim, data.Bodies);
        memoryBufferHandler.WriteViewToMemory(physicsEngine.View);

        Logger.Entry().Log("Preset Imported");
    }

    internal static PresetData? ParseJsonPreset(string jsonPreset)
    {
        PresetData? data = JsonSerializer.Deserialize(
                jsonPreset,
                PresetJSONSerializerContext.Default.PresetData
            );
        return data;
    }

    [JSExport]
    public static string[] GetLogs(int number = -1) => Logger.GetLogs(number);

    [JSExport]
    public static void ClearLogs() => Logger.Clear();

    #endregion
}

internal static class Logger
{
    #region Configuration
    private const int MaxLogs = 1000;

    private const string TimeStampFormat = "yyyy/MM/dd-HH:mm:ss.fff 'UTC'";
    private const string ArgsListOpen = "with args:[";
    private const char ArgsListClose = ']';
    private const string ArgsSeparator = ", ";
    private const char NameValueSeparator = '=';
    #endregion

    private static readonly string[] _logs = new string[MaxLogs];
    private static int _nextWriteIndex = 0;
    private static int _count = 0;

    /// <summary>
    /// Gets a number of logged entries.
    /// </summary>
    /// <param name="number">The number of logs to get. -1 (or any other negative number) to get all logs.</param>
    /// <returns>An array of logged strings, from oldest to newest.</returns>
    public static string[] GetLogs(int number = -1)
    {
        if (number == 0 || _count == 0) return [];
        var amount = number < 0 ? _count : Math.Min(number, _count);

        string[] requestedLogs = new string[amount];

        for (int i = 0; i < amount; i++)
        {
            // Start at the oldest log that should be retrieved and go from there.
            // Adding _logs.Length avoids a modulo of a potentially negative number.
            var logIdx = (_nextWriteIndex - amount + i + _logs.Length) % _logs.Length;
            requestedLogs[i] = _logs[logIdx];
        }

        return requestedLogs;
    }

    /// <summary>
    /// Clears the currently stored log entries.
    /// </summary>
    public static void Clear()
    {
        _nextWriteIndex = 0;
        _count = 0;
    }

    /// <inheritdoc cref="LogEntry"/>
    public static LogEntry Entry([CallerMemberName] string callingMemberName = "") => new(callingMemberName);

    private static void AddLogEntry(string message)
    {
        _logs[_nextWriteIndex] = message;
        _nextWriteIndex = (_nextWriteIndex + 1) % _logs.Length;
        if (_count < _logs.Length) _count++;
    }

    /// <summary>
    /// A log entry, which acts as a builder for the final logged string.
    /// <example>
    /// <code>
    /// // Log without arguments
    /// Logger.Entry().Log("Lorem ipsum");
    /// 
    /// // Log with arguments
    /// void UpdateName(int id, string? name = null) 
    /// {
    ///     string nameAfterUpdate = UpdateNameWorker(id, name);
    ///     Logger.Entry().WithArg(id).WithArg(name).Log($"New name = {nameAfterUpdate}");
    /// }
    /// </code>
    /// </example>
    /// </summary>
    /// <param name="callingMemberName">Provided by the compiler but may be supplied explicitly.</param>
    public ref struct LogEntry([CallerMemberName] string callingMemberName = "")
    {
        private static readonly CompositeFormat _compositeFormat = CompositeFormat.Parse("{0:" + TimeStampFormat + "}");

        private readonly StringBuilder _builder = new StringBuilder()
            .AppendFormat(CultureInfo.InvariantCulture, _compositeFormat, DateTime.UtcNow)
            .Append(" [").Append(callingMemberName).Append(']');

        private bool _hasArg = false;
        private bool _isLogged = false;

        /// <summary>
        /// Adds the given argument to the entry if the argument is not null.
        /// </summary>
        /// <param name="value">The value of the argument. Null values are silently ignored.</param>
        /// <param name="name">The source code expression for the 'value', captured by the compiler.</param>
        public LogEntry WithArg<T>(T? value, [CallerArgumentExpression(nameof(value))] string name = "")
        {
            if (value is not null)
            {
                if (_hasArg) _builder.Append(ArgsSeparator);
                else
                {
                    _builder.Append(' ').Append(ArgsListOpen);
                    _hasArg = true;
                }
                _builder.Append(name).Append(NameValueSeparator).Append(value);
            }
            return this;
        }

        /// <summary>
        /// Adds the entry to the <see cref="Logger"/>.
        /// </summary>
        /// <param name="message">The message of this entry.</param>
        /// <remarks>
        /// An entry can only be logged once; Calling this method again will not log it again.
        /// </remarks>
        public void Log(string message = "")
        {
            if (_isLogged) return;

            if (_hasArg) _builder.Append(ArgsListClose);
            if (message != "") _builder.Append(' ').Append(message);

            AddLogEntry(_builder.ToString());
            _isLogged = true;
        }
    }
}
