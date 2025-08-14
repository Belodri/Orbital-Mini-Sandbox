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
    /// The mass of the body in Solar Masses (M☉).
    /// </summary>
    double Mass { get; }
    /// <summary>
    /// The position of the body in space in au at time t.
    /// </summary>
    Vector2D Position { get; }
    /// <summary>
    /// The velocity vector of the body in space in au/d at time t.
    /// </summary>
    Vector2D Velocity { get; }
    /// <summary>
    /// The velocity vector of the body in space in au/d at time (t - Δt/2).
    /// </summary>
    Vector2D VelocityHalfStep { get; }
    /// <summary>
    /// The acceleration vector of the body in au/d² at time t.
    /// </summary>
    Vector2D Acceleration { get; }
    /// <summary>
    /// An event raised after the body's Enabled status has changed.
    /// </summary>
    /// <remarks>
    /// This event only fires if the property's value is different after the update.
    /// </remarks>
    event Action<ICelestialBody>? EnabledChanged;
    /// <summary>
    /// Atomically updates one or more properties of the celestial body.
    /// Unspecified (<c>null</c>) parameters will be ignored and their corresponding properties will remain unchanged.
    /// </summary>
    /// <param name="enabled">The new value for the <see cref="Enabled"/> property. If null, the current value is not changed.</param>
    /// <param name="mass">The new value for the <see cref="Mass"/> property. If null, the current value is not changed.</param>
    /// <param name="posX">The new X component for the <see cref="Position"/> vector. If null, the X component is not changed.</param>
    /// <param name="posY">The new Y component for the <see cref="Position"/> vector. If null, the Y component is not changed.</param>
    /// <param name="velX">The new X component for the <see cref="Velocity"/> vector. If null, the X component is not changed.</param>
    /// <param name="velY">The new Y component for the <see cref="Velocity"/> vector. If null, the Y component is not changed.</param>
    /// <param name="velX_half">The new X component for the <see cref="VelocityHalfStep"/> vector. If null, the X component is not changed.</param>
    /// <param name="velY_half">The new Y component for the <see cref="VelocityHalfStep"/> vector. If null, the Y component is not changed.</param>
    /// <param name="accX">The new X component for the <see cref="Acceleration"/> vector. If null, the X component is not changed.</param>
    /// <param name="accY">The new Y component for the <see cref="Acceleration"/> vector. If null, the Y component is not changed.</param>
    void Update(
        bool? enabled = null,
        double? mass = null,
        double? posX = null,
        double? posY = null,
        double? velX = null,
        double? velY = null,
        double? velX_half = null,
        double? velY_half = null,
        double? accX = null,
        double? accY = null
    );

    /// <inheritdoc cref="Update(bool?, double?, double?, double?, double?, double?, double?, double?, double?, double?)"/>
    /// <param name="position">The new value for the <see cref="Position"/> vector. If null, the current value is not changed.</param>
    /// <param name="velocity">The new value for the <see cref="Velocity"/> vector. If null, the current value is not changed.</param>
    /// <param name="velocityHalfStep">The new value for the <see cref="VelocityHalfStep"/> vector. If null, the current value is not changed.</param>
    /// <param name="acceleration">The new value for the <see cref="Acceleration"/> vector. If null, the current value is not changed.</param>
    void Update(
        bool? enabled = null,
        double? mass = null,
        Vector2D? position = null,
        Vector2D? velocity = null,
        Vector2D? velocityHalfStep = null,
        Vector2D? acceleration = null
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
        Vector2D? velocity = null,
        Vector2D? velocityHalfStep = null,
        Vector2D? acceleration = null)
    {
        if (id < 0) throw new ArgumentOutOfRangeException(nameof(id), "Id must not be negative.");
        Id = id;
        Enabled = enabled;
        Mass = mass;

        if (position == null)
        {
            var offset = id * INITIALIZATION_POSITION_OFFSET + INITIALIZATION_POSITION_OFFSET;
            Position = new(offset, offset);
        }
        else Position = position.Value;

        Velocity = velocity ?? Vector2D.Zero;
        VelocityHalfStep = velocityHalfStep ?? Vector2D.Zero;
        Acceleration = acceleration ?? Vector2D.Zero;
    }

    #endregion


    #region Consts & Config

    /// <summary>
    /// This deterministic offset ensures no two bodies are ever initialized at exactly (0, 0).
    /// </summary>
    private const double INITIALIZATION_POSITION_OFFSET = 1e-10;

    #endregion


    #region Fields & Properties

    /// <inheritdoc />
    public int Id { get; init; }
    /// <inheritdoc />
    public bool Enabled
    {
        get;
        private set
        {
            if (value != field)
            {
                field = value;
                EnabledChanged?.Invoke(this);
            }
        }
    }
    /// <inheritdoc />
    public double Mass { get; private set; }
    /// <inheritdoc />
    public Vector2D Position { get; private set; }
    /// <inheritdoc />
    public Vector2D Velocity { get; private set; }
    /// <inheritdoc />
    public Vector2D VelocityHalfStep { get; private set; }
    /// <inheritdoc />
    public Vector2D Acceleration { get; private set; }

    #endregion

    /// <inheritdoc />
    public event Action<ICelestialBody>? EnabledChanged;

    #region Updates

    /// <inheritdoc />
    public void Update(
        bool? enabled = null,
        double? mass = null,
        double? posX = null,
        double? posY = null,
        double? velX = null,
        double? velY = null,
        double? velX_half = null,
        double? velY_half = null,
        double? accX = null,
        double? accY = null)
    {
        Enabled = enabled ?? Enabled;
        Mass = mass ?? Mass;
        Position = Position.With(posX, posY);
        Velocity = Velocity.With(velX, velY);
        VelocityHalfStep = VelocityHalfStep.With(velX_half, velY_half);
        Acceleration = Acceleration.With(accX, accY);
    }

    /// <inheritdoc />
    public void Update(
        bool? enabled = null,
        double? mass = null,
        Vector2D? position = null,
        Vector2D? velocity = null,
        Vector2D? velocityHalfStep = null,
        Vector2D? acceleration = null)
    {
        Enabled = enabled ?? Enabled;
        Mass = mass ?? Mass;
        Position = Position.With(position);
        Velocity = Velocity.With(velocity);
        VelocityHalfStep = VelocityHalfStep.With(velocityHalfStep);
        Acceleration = Acceleration.With(acceleration);
    }

    #endregion
}
