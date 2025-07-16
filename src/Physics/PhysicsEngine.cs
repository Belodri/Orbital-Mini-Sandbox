using Physics.Bodies;
using Physics.Core;
using Physics.Models;

namespace Physics;

// Public facing records. Cannot introduce changes that break consumer code!
#region Public DTO Records

// Preset Data DTOs
// Cannot contain derived data.
public record PresetBodyData(int Id, bool Enabled, double Mass, double PosX, double PosY, double VelX, double VelY);
public record PresetSimData(double SimulationTime, double TimeScale, bool IsTimeForward, int TimeConversionFactor);
public record PresetData(PresetSimData PresetSimData, PresetBodyData[] PresetBodyDataArray);

// Tick Data DTOs
// Can contain derived data.
public record BodyTickData(int Id, bool Enabled, double Mass, double PosX, double PosY, double VelX, double VelY);
public record SimTickData(double SimulationTime, double TimeScale, bool IsTimeForward, int TimeConversionFactor);
public record TickData(SimTickData SimTickData, BodyTickData[] BodyTickDataArray);

// Update Data DTOs
// Optional arguments must be nullable!
public record UpdateBodyData(int Id, bool? Enabled, double? Mass, double? PosX, double? PosY, double? VelX, double? VelY);
public record UpdateSimData(double? SimulationTime, double? TimeScale, bool? IsTimeForward, int? TimeConversionFactor);

#endregion


#region Internal DTO Records

internal record BodyData(int Id, bool Enabled, double Mass, Vector2D Position, Vector2D Velocity);
internal record BodyDataPartial(int Id, bool? Enabled, double? Mass, double? PosX, double? PosY, double? VelX, double? VelY);

internal record TimerData(double SimulationTime, double TimeScale, bool IsTimeForward, int TimeConversionFactor);
internal record TimerDataPartial(double SimulationTime, double TimeScale, bool IsTimeForward, int TimeConversionFactor);

internal record SimulationData(TimerData TimerData, BodyData[] BodyDataArray);

internal record BodyTickUpdateDTO(CelestialBody Body, BodyDataPartial UpdateData);

internal record CalculatorData(double Theta, double GravitationalConstant, double SofteningFactor);

#endregion


public class PhysicsEngine
{
    #region Fields & Properties

    internal Simulation simulation = new();

    #endregion


    #region Tick

    public TickData Tick(double deltaTime)
    {
        simulation.Tick(deltaTime);
        return simulation.ToTickData();
    }

    public TickData GetTickData() => simulation.ToTickData();

    #endregion


    #region Body Handling

    public int CreateBody() => simulation.CreateBody().Id;

    public bool DeleteBody(int id) => simulation.DeleteBody(id);

    public bool UpdateBody(UpdateBodyData updatePreset)
    {
        var body = simulation.TryGetBody(updatePreset.Id);
        return body != null && body.Update(updatePreset.ToBodyDataPartial());
    }

    #endregion


    #region Preset Handling

    public PresetData GetPresetData() => simulation.ToPresetData();

    public TickData LoadPreset(PresetData preset)
    {
        simulation = new(preset.ToSimulationData());
        return simulation.ToTickData();
    }

    #endregion
}


internal static class DTOMapper
{
    #region Public to Internal

    public static SimulationData ToSimulationData(this PresetData data)
        => new(data.PresetSimData.ToTimerData(), [.. data.PresetBodyDataArray.Select(ToBodyData)]);

    public static TimerData ToTimerData(this PresetSimData data)
        => new(data.SimulationTime, data.TimeScale, data.IsTimeForward, data.TimeConversionFactor);

    public static BodyData ToBodyData(this PresetBodyData data)
        => new(data.Id, data.Enabled, data.Mass, new(data.PosX, data.PosY), new(data.VelX, data.VelY));

    public static BodyDataPartial ToBodyDataPartial(this UpdateBodyData data)
        => new(data.Id, data.Enabled, data.Mass, data.PosX, data.PosY, data.VelX, data.VelY);

    #endregion


    #region Internal to Public

    public static TickData ToTickData(this Simulation sim)
        => new(sim.ToSimTickData(), [.. sim.Bodies.Values.Select(ToBodyTickData)]);

    public static SimTickData ToSimTickData(this Simulation sim)
        => new(
            sim.Timer.SimulationTime,
            sim.Timer.TimeScale,
            sim.Timer.IsTimeForward,
            sim.Timer.TimeConversionFactor
        );

    public static PresetData ToPresetData(this Simulation sim)
        => new(sim.ToPresetSimData(), [.. sim.Bodies.Values.Select(ToPresetBodyData)]);

    public static PresetSimData ToPresetSimData(this Simulation sim)
        => new(
            sim.Timer.SimulationTime,
            sim.Timer.TimeScale,
            sim.Timer.IsTimeForward,
            sim.Timer.TimeConversionFactor
        );
    
    public static PresetBodyData ToPresetBodyData(this CelestialBody body)
        => new(body.Id, body.Enabled, body.Mass, body.Position.X, body.Position.Y, body.Velocity.X, body.Velocity.Y);


    public static BodyTickData ToBodyTickData(this CelestialBody body)
        => new(body.Id, body.Enabled, body.Mass, body.Position.X, body.Position.Y, body.Velocity.X, body.Velocity.Y);

    public static UpdateBodyData ToUpdateBodyData(this BodyDataPartial data)
        => new(data.Id, data.Enabled, data.Mass, data.PosX, data.PosY, data.VelX, data.VelY);

    
    #endregion
}