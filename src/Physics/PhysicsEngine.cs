using Physics.Core;

namespace Physics;

#region Tick Data DTO Records

public record BodyTickData(int Id, bool Enabled, double Mass, double PosX, double PosY, double VelX, double VelY);

public record SimTickData(double SimulationTime, double TimeScale, bool IsTimeForward);

public record TickData(SimTickData SimTickData, BodyTickData[] BodyTickDataArray);

#endregion


#region Preset Data DTO Records

public record PresetBodyData(int Id, bool Enabled, double Mass, double PosX, double PosY, double VelX, double VelY);

public record PresetSimData(double SimulationTime, double TimeScale, bool IsTimeForward);

public record PresetData(PresetSimData PresetSimData, PresetBodyData[] PresetBodyDataArray);

#endregion

#region Other DTO Records

public record BodyUpdateData(int Id, bool? Enabled, double? Mass, double? PosX, double? PosY, double? VelX, double? VelY);

#endregion


public class PhysicsEngine
{
    internal Simulation simulation = new(Simulation.DEFAULT_PRESET_DATA);

    #region Public Methods

    public TickData Tick(double deltaTime) => simulation.Tick(deltaTime).GetTickData();

    public PresetData GetPresetData() => simulation.GetPresetData();

    public TickData LoadPreset(PresetData preset)
    {
        simulation = new(preset);
        return simulation.GetTickData();
    }

    public TickData GetTickData() => simulation.GetTickData();

    public int CreateBody() => simulation.AddNewBody().Id;

    public bool DeleteBody(int id) => simulation.DeleteBody(id);

    public bool UpdateBody(BodyUpdateData updatePreset) => simulation.UpdateBody(updatePreset);

    public BodyTickData? GetBodyTickData(int id) => simulation.GetBodyTickData(id);

    #endregion
}
