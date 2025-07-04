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
    internal Vector2D Position { get; set; } = presetData.PosX == 0 && presetData.PosY == 0
        ? new(1e-15 * presetData.Id, 1e-15 * presetData.Id)     // ensure that no two bodies are ever initialized in the exact same position
        : new(presetData.PosX, presetData.PosY);
    internal Vector2D Velocity { get; set; } = new(presetData.VelX, presetData.VelY);

    #region DTOs

    internal BodyTickData GetBodyTickData() => new(Id, Enabled, Mass, Position.X, Position.Y, Velocity.X, Velocity.Y);
    internal PresetBodyData GetPresetBodyData() => new(Id, Enabled, Mass, Position.X, Position.Y, Velocity.X, Velocity.Y);

    #endregion

    internal bool Update(BodyUpdateData updatePreset)   // TODO: Add validation logic for updatePreset and return false without updating if invalid
    {
        Enabled = updatePreset.Enabled ?? Enabled;
        Mass = updatePreset.Mass ?? Mass;

        if ((updatePreset.PosX.HasValue && updatePreset.PosX.Value != Position.X)
            || (updatePreset.PosY.HasValue && updatePreset.PosY.Value != Position.Y))
        {
            Position = new(
                updatePreset.PosX ?? Position.X,
                updatePreset.PosY ?? Position.Y
            );
        }

        if ((updatePreset.VelX.HasValue && updatePreset.VelX.Value != Velocity.X) 
            || (updatePreset.VelY.HasValue && updatePreset.VelY.Value != Velocity.Y))
        {
            Velocity = new(
                updatePreset.VelX ?? Velocity.X,
                updatePreset.VelY ?? Velocity.Y
            );
        }

        return true;
    }
}
