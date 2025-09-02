using Physics.Models;

namespace Physics.Bodies;

internal interface ICelestialBody
{
    /// <summary>
    /// A limit of ~16 million light years in any direction from the origin.
    /// </summary>
    /// <remarks>A body beyond this point is considered out of bounds.</remarks>
    internal const double LIMIT = 1e12;
    /// <summary>
    /// Unique ID of the body. Must not be negative!
    /// </summary>
    int Id { get; }
    /// <summary>
    /// A disabled body will be ignored by physics calculations.
    /// </summary>
    bool Enabled { get; }
    /// <summary>
    /// Is the body's position outside the position limit?
    /// </summary>
    bool OutOfBounds { get; }
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
    /// Atomically updates one or more properties of the celestial body.
    /// </summary>
    /// <param name="updates">Partial data to update a celestial body. Null values are ignored.</param>
    void Update(BodyDataUpdates updates);
    /// <inheritdoc cref="Update(BodyDataUpdates)"/>
    /// <param name="position">The new value for the <see cref="Position"/> vector. If null, the current value is not changed.</param>
    /// <param name="velocity">The new value for the <see cref="Velocity"/> vector. If null, the current value is not changed.</param>
    /// <param name="velocityHalfStep">The new value for the <see cref="VelocityHalfStep"/> vector. If null, the current value is not changed.</param>
    /// <param name="acceleration">The new value for the <see cref="Acceleration"/> vector. If null, the current value is not changed.</param>
    /// <remarks>
    /// Unspecified (<c>null</c>) parameters will be ignored and their corresponding properties will remain unchanged.
    /// </remarks>
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
        Vector2D? acceleration = null
    )
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

    /// <inheritdoc cref="ICelestialBody.LIMIT"/> 
    public const double POSITION_LIMIT_ABSOLUTE = ICelestialBody.LIMIT;

    #endregion


    #region Fields & Properties

    /// <inheritdoc />
    public int Id { get; init; }
    /// <inheritdoc />
    public bool Enabled { get; private set; }
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
    /// <inheritdoc />
    public bool OutOfBounds => Math.Abs(Position.X) > POSITION_LIMIT_ABSOLUTE || Math.Abs(Position.Y) > POSITION_LIMIT_ABSOLUTE;

    #endregion

    #region Updates

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

    /// <inheritdoc />
    public void Update(BodyDataUpdates updates)
    {
        Enabled = updates.Enabled ?? Enabled;
        Mass = updates.Mass ?? Mass;
        Position = Position.With(updates.PosX, updates.PosY);
        Velocity = Velocity.With(updates.VelX, updates.VelY);
        Acceleration = Acceleration.With(updates.AccX, updates.AccY);
    }

    #endregion
}
