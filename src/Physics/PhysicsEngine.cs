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


public class PhysicsEngine
{
    internal Simulation simulation = new();


    #region Public Methods

    public TickData Tick(double timestamp)
    {
        return simulation.GetTickData();
    }

    public PresetData GetPresetData()
    {
        return simulation.GetPresetData();
    }

    public TickData LoadPreset(PresetData preset)
    {
        simulation = Simulation.CreateFromPreset(preset);
        return simulation.GetTickData();
    }

    public int CreateBody() => simulation.AddNewBody().Id;

    public bool DeleteBody(int id) => simulation.DeleteBody(id);

    public bool UpdateBody(PresetBodyData updatePreset) => simulation.UpdateBody(updatePreset);

    public void CreateTestSim(int bodyCount)
    {
        simulation = Simulation.CreateDefault(bodyCount);
    }

    public BodyTickData? GetBodyTickData(int id)
    {
        return simulation.Bodies.TryGetValue(id, out var body)
            ? body.GetBodyTickData()
            : null;
    }

    #endregion
}
