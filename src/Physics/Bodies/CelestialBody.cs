using Physics.Models;

namespace Physics.Bodies;

internal class CelestialBody
{
    #region Constructors

    internal CelestialBody(int id) : this(CreateInitialData(id)) {}

    internal CelestialBody(BodyData data)
    {
        if (data.Id <= 0) throw new ArgumentOutOfRangeException(nameof(data), "Id must be greater than 0.");
        Id = data.Id;
        _enabled = data.Enabled;
        _mass = data.Mass;
        _position = data.Position;
        _velocity = data.Velocity;
    }

    private static BodyData CreateInitialData(int id)
    {
        var offset = id * INITIALIZATION_POSITION_OFFSET;
        return DEFAULT_DATA with { Id = id, Position = new(offset, offset) };
    }

    #endregion


    #region Consts & Config

    internal static readonly BodyData DEFAULT_DATA = new(0, false, 0.0, new(0, 0), new(0, 0));

    /// <summary>
    /// This deterministic offset ensures no two bodies are ever initialized at exactly (0, 0).
    /// </summary>
    internal const double INITIALIZATION_POSITION_OFFSET = 1e-10;

    #endregion


    #region Fields & Properties

    bool _enabled;
    double _mass;
    Vector2D _position;
    Vector2D _velocity;

    internal int Id { get; init; }
    internal bool Enabled { get => _enabled; private set => _enabled = value; }
    internal double Mass { get => _mass; private set => _mass = value; }
    internal Vector2D Position { get => _position; private set => _position = value; }
    internal Vector2D Velocity { get => _velocity; private set => _velocity = value; }

    #endregion


    #region Updates

    internal event Action<CelestialBody, BodyDataPartial>? Updated;

    internal bool Update(BodyDataPartial updatePreset)
    {
        var enabledChanged = TryUpdateProperty(ref _enabled, updatePreset.Enabled);
        var massChanged = TryUpdateProperty(ref _mass, updatePreset.Mass);
        var posChanged = TryUpdateProperty(ref _position, updatePreset.PosX, updatePreset.PosY);
        var velChanged = TryUpdateProperty(ref _velocity, updatePreset.VelX, updatePreset.VelY);

        bool anyPropertyChanged = enabledChanged || massChanged || posChanged || velChanged;
        if (anyPropertyChanged)
        {
            BodyDataPartial delta = new(
                Id,
                enabledChanged ? Enabled : null,
                massChanged ? Mass : null,
                posChanged ? Position.X : null,
                posChanged ? Position.Y : null,
                velChanged ? Velocity.X : null,
                velChanged ? Velocity.Y : null
            );
            Updated?.Invoke(this, delta);
        }

        return true; // TODO: Add validation logic for updatePreset and return false without updating if invalid
    }

    /// <summary>
    /// Updates a property if it's different.
    /// </summary>
    /// <param name="currentValue">A reference to the field/property being updated.</param>
    /// <param name="newValue">The potential new value.</param>
    /// <returns>True if the property was changed, otherwise false.</returns>
    private static bool TryUpdateProperty<T>(ref T currentValue, T? newValue) where T : struct, IEquatable<T>
    {
        if (newValue.HasValue && !newValue.Value.Equals(currentValue))
        {
            currentValue = newValue.Value;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Updates a Vector2D property from its component parts if it's different.
    /// </summary>
    /// <param name="currentValue">A reference to the field/property being updated.</param>
    /// <param name="newX">The potential new value for the X component.</param>
    /// <param name="newY">The potential new value for the Y component.</param>
    /// <returns>True if the property was changed, otherwise false.</returns>
    private static bool TryUpdateProperty(ref Vector2D currentValue, double? newX, double? newY)
    {
        var xChange = newX != null && newX.Value != currentValue.X;
        var yChange = newY != null && newY.Value != currentValue.Y;
        if (!xChange && !yChange) return false;

        currentValue = new(
            newX ?? currentValue.X,
            newY ?? currentValue.Y
        );
        return true;
    }

    #endregion
}
