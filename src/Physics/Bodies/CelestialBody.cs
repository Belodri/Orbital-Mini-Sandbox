using Physics.Models;

namespace Physics.Bodies;

internal class CelestialBody(int id, bool enabled, double mass, Vector2D position, Vector2D velocity)
{
    #region Factory Methods

    internal static CelestialBody CreateFromPreset(PresetBodyData presetBodyData)
    {
        Vector2D position = new(presetBodyData.PosX, presetBodyData.PosY);
        Vector2D velocity = new(presetBodyData.VelX, presetBodyData.VelY);
        return new(presetBodyData.Id, presetBodyData.Enabled, presetBodyData.Mass, position, velocity);
    }

    internal static CelestialBody CreateDefault(int id)
    {
        Vector2D position = new(0.0, 0.0);
        Vector2D velocity = new(0.0, 0.0);
        var enabled = false;    // MUST BE FALSE!!!
        var mass = 1.0;
        return new(id, enabled, mass, position, velocity);
    }

    #endregion

    internal int Id { get; init; } = id;
    internal bool Enabled { get; set; } = enabled;
    internal double Mass { get; set; } = mass;
    internal Vector2D Position { get; set; } = position;
    internal Vector2D Velocity { get; set; } = velocity;

    double PosX => Position.X;
    double PosY => Position.Y;
    double VelX => Velocity.X;
    double VelY => Velocity.Y;

    #region DTOs

    internal BodyTickData GetBodyTickData()
    {
        return new(Id, Enabled, Mass, PosX, PosY, VelX, VelY);
    }

    internal PresetBodyData GetPresetBodyData()
    {
        return new(Id, Enabled, Mass, PosX, PosY, VelX, VelY);
    }

    #endregion
}
