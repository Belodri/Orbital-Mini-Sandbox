using Physics.Bodies;

namespace Physics.Core;

internal class Simulation
{
    #region Factory Methods

    internal static Simulation CreateFromPreset(PresetData presetData)
    {
        Simulation sim = new()
        {
            TimeScale = presetData.PresetSimData.TimeScale,
            SimulationTime = presetData.PresetSimData.SimulationTime,
            IsTimeForward = presetData.PresetSimData.IsTimeForward
        };

        foreach (PresetBodyData bodyState in presetData.PresetBodyDataArray)
        {
            sim.Bodies.Add(bodyState.Id, CelestialBody.CreateFromPreset(bodyState));
        }

        return sim;
    }

    internal static Simulation CreateDefault(int bodyCount = 5)
    {
        Simulation sim = new()
        {
            TimeScale = 1.0,
            SimulationTime = 0.0,
            IsTimeForward = true
        };

        for (int i = 0; i < bodyCount; i++) sim.AddNewBody();

        return sim;
    }

    #endregion

    internal double SimulationTime { get; set; }
    internal double TimeScale { get; set; }
    internal bool IsTimeForward { get; set; }

    static readonly Random rnd = new();

    internal Dictionary<int, CelestialBody> Bodies = [];

    internal CelestialBody AddNewBody()
    {
        int validId = -1;
        while (validId < 0)
        {
            int Id = rnd.Next();
            if (!Bodies.ContainsKey(Id)) validId = Id;
        }

        var body = CelestialBody.CreateDefault(validId);
        Bodies.Add(body.Id, body);
        return body;
    }

    #region DTOs

    internal TickData GetTickData()
    {
        return new(
            GetSimTickData(),
            [.. Bodies.Values.Select(b => b.GetBodyTickData())]
        );
    }

    internal SimTickData GetSimTickData()
    {
        return new(SimulationTime, TimeScale, IsTimeForward);
    }

    internal PresetData GetPresetData()
    {
        return new(
            GetPresetSimData(),
            [.. Bodies.Values.Select(b => b.GetPresetBodyData())]
        );
    }

    internal PresetSimData GetPresetSimData()
    {
        return new(SimulationTime, TimeScale, IsTimeForward);
    }

    #endregion
}
