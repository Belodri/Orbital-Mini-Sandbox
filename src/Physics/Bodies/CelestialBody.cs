using Physics.Models;

namespace Physics.Bodies;

internal class CelestialBody(PresetBodyData presetData)
{
    internal static readonly PresetBodyData DEFAULT_PRESET_DATA = new(-1, false, 0.0, 0.0, 0.0, 0.0, 0.0);

    // Constructor Params
    internal int Id { get; init; } = presetData.Id >= 0
        ? presetData.Id
        : throw new ArgumentOutOfRangeException(nameof(presetData.Id), "Id must be a non-negative integer.");
    internal bool Enabled { get; set; } = presetData.Enabled;
    internal double Mass { get; set; } = presetData.Mass;
    internal Vector2D Position { get; set; } = new(presetData.PosX, presetData.PosY);
    internal Vector2D Velocity { get; set; } = new(presetData.VelX, presetData.VelY);

    #region DTOs

    internal BodyTickData GetBodyTickData() => new(Id, Enabled, Mass, Position.X, Position.Y, Velocity.X, Velocity.Y);
    internal PresetBodyData GetPresetBodyData() => new(Id, Enabled, Mass, Position.X, Position.Y, Velocity.X, Velocity.Y);

    #endregion

    internal bool Update(PresetBodyData updatePreset)   // TODO: Add validation logic for updatePreset and return false without updating if invalid
    {
        Enabled = updatePreset.Enabled;
        Mass = updatePreset.Mass;

        if (updatePreset.PosX != Position.X || updatePreset.PosY != Position.Y)
        {
            Position = new(updatePreset.PosX, updatePreset.PosY);
        }

        if (updatePreset.VelX != Velocity.X || updatePreset.VelY != Velocity.Y)
        {
            Velocity = new(updatePreset.VelX, updatePreset.VelY);
        }

        return true;
    }
}
