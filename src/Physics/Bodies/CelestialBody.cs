using Physics.Models;

namespace Physics.Bodies;

internal interface ICelestialBody
{
    /// <summary>
    /// Unique ID of the body. Must not be negative!
    /// </summary>
    int Id { get; }

    /// <summary>
    /// A disabled body will be ignored by physics calculations.
    /// </summary>
    bool Enabled { get; }

    /// <summary>
    /// The mass of the body in kg.
    /// </summary>
    double Mass { get; }

    /// <summary>
    /// The position of the body in space.
    /// </summary>
    Vector2D Position { get; }

    /// <summary>
    /// The velocity vector of the body in space.
    /// </summary>
    Vector2D Velocity { get; }

    /// <summary>
    /// An event raised after the body's state has been successfully changed via the <see cref="Update"/> method.
    /// </summary>
    /// <remarks>
    /// This event only fires if at least one property's value is different after the update.
    /// The event handler receives two arguments: the instance of the body that was updated, and a <see cref="BodyDataUpdates"/>
    /// record containing only the new values for the properties that were changed. Unchanged properties will be <c>null</c> in this record.
    /// </remarks>
    event Action<ICelestialBody, BodyDataUpdates>? Updated;

    /// <summary>
    /// Atomically updates one or more properties of the celestial body.
    /// If any property's value changes, the <see cref="Updated"/> event is raised.
    /// Unspecified (<c>null</c>) parameters will be ignored and their corresponding properties will remain unchanged.
    /// </summary>
    /// <param name="enabled">The new value for the <see cref="Enabled"/> property. If null, the current value is not changed.</param>
    /// <param name="mass">The new value for the <see cref="Mass"/> property. If null, the current value is not changed.</param>
    /// <param name="posX">The new X component for the <see cref="Position"/> vector. If null, the X component is not changed.</param>
    /// <param name="posY">The new Y component for the <see cref="Position"/> vector. If null, the Y component is not changed.</param>
    /// <param name="velX">The new X component for the <see cref="Velocity"/> vector. If null, the X component is not changed.</param>
    /// <param name="velY">The new Y component for the <see cref="Velocity"/> vector. If null, the Y component is not changed.</param>
    void Update(
        bool? enabled = null,
        double? mass = null,
        double? posX = null,
        double? posY = null,
        double? velX = null,
        double? velY = null
    );

    /// <inheritdoc cref="Update(bool?, double?, double?, double?, double?, double?)"/>
    /// <param name="position">The new value for the <see cref="Position"/> vector. If null, the current value is not changed.</param>
    /// <param name="velocity">The new value for the <see cref="Velocity"/> vector. If null, the current value is not changed.</param>
    void Update(
        bool? enabled = null,
        double? mass = null,
        Vector2D? position = null,
        Vector2D? velocity = null
    );
}

internal class CelestialBody : ICelestialBody
{
    #region Constructors

    internal CelestialBody(
        int id,
        bool enabled = false,
        double mass = 0.0,
        Vector2D? position = null,
        Vector2D? velocity = null)
    {
        if (id < 0) throw new ArgumentOutOfRangeException(nameof(id), "Id must not be negative.");
        Id = id;
        _enabled = enabled;
        _mass = mass;

        if (position == null)
        {
            var offset = id * INITIALIZATION_POSITION_OFFSET + INITIALIZATION_POSITION_OFFSET;
            _position = new(offset, offset);
        }
        else _position = position.Value;

        _velocity = velocity ?? Vector2D.Zero;
    }

    #endregion


    #region Consts & Config

    /// <summary>
    /// This deterministic offset ensures no two bodies are ever initialized at exactly (0, 0).
    /// </summary>
    private const double INITIALIZATION_POSITION_OFFSET = 1e-10;

    #endregion


    #region Fields & Properties

    bool _enabled;
    double _mass;
    Vector2D _position;
    Vector2D _velocity;

    /// <inheritdoc />
    public int Id { get; init; }
    /// <inheritdoc />
    public bool Enabled { get => _enabled; private set => _enabled = value; }
    /// <inheritdoc />
    public double Mass { get => _mass; private set => _mass = value; }
    /// <inheritdoc />
    public Vector2D Position { get => _position; private set => _position = value; }
    /// <inheritdoc />
    public Vector2D Velocity { get => _velocity; private set => _velocity = value; }

    #endregion


    #region Updates

    /// <inheritdoc />
    public event Action<ICelestialBody, BodyDataUpdates>? Updated;

    /// <inheritdoc />
    public void Update(
        bool? enabled = null,
        double? mass = null,
        double? posX = null,
        double? posY = null,
        double? velX = null,
        double? velY = null)
    {
        var enabledChanged = TryUpdateProperty(ref _enabled, enabled);
        var massChanged = TryUpdateProperty(ref _mass, mass);
        var posChanged = TryUpdateProperty(ref _position, posX, posY);
        var velChanged = TryUpdateProperty(ref _velocity, velX, velY);
        UpdateNotificationWorker(enabledChanged, massChanged, posChanged, velChanged);
    }

    /// <inheritdoc />
    public void Update(
        bool? enabled = null,
        double? mass = null,
        Vector2D? position = null,
        Vector2D? velocity = null)
    {
        var enabledChanged = TryUpdateProperty(ref _enabled, enabled);
        var massChanged = TryUpdateProperty(ref _mass, mass);
        var posChanged = TryUpdateProperty(ref _position, position);
        var velChanged = TryUpdateProperty(ref _position, position);
        UpdateNotificationWorker(enabledChanged, massChanged, posChanged, velChanged);
    }

    /// <summary>
    /// Notifies the subscribers of this body's update event based on the fields that were updated.
    /// </summary>
    /// <param name="enabledChanged">Was the <see cref="Enabled"/> field updated?</param>
    /// <param name="massChanged">Was the <see cref="Mass"/> field updated?</param>
    /// <param name="posChanged">Was the <see cref="Position"/> field or one of its components updated?</param>
    /// <param name="velChanged">Was the <see cref="Velocity"/> field or one of its components updated?</param>
    private void UpdateNotificationWorker(bool enabledChanged, bool massChanged, bool posChanged, bool velChanged)
    {
        bool anyPropertyChanged = enabledChanged || massChanged || posChanged || velChanged;
        if (!anyPropertyChanged) return;

        BodyDataUpdates delta = new(
            Enabled: enabledChanged ? _enabled : null,
            Mass: massChanged ? _mass : null,
            PosX: posChanged ? _position.X : null,
            PosY: posChanged ? _position.Y : null,
            VelX: velChanged ? _velocity.X : null,
            VelY: velChanged ? _velocity.Y : null
        );
        Updated?.Invoke(this, delta);
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
