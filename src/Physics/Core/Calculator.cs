using System.Diagnostics.CodeAnalysis;
using Physics.Bodies;
using Physics.Models;

namespace Physics.Core;

internal interface ICalculator
{
    /// <summary>
    /// The value for the gravitational constant G in cubic meters per kilogram per second squared.
    /// </summary>
    /// <value>The gravitational constant, in units of m³/kg/s².</value>
    double GravitationalConstant { get; }

    /// <summary>
    /// The opening-angle parameter (theta, θ) for the Barnes-Hut algorithm.
    /// </summary>
    /// <value>A value, between 0 and 1 (inclusive), that controls the trade-off between accuracy and computational speed.</value>
    /// <remarks>
    /// A smaller theta value results in higher accuracy but more calculations, as tree nodes must be closer to be treated as a single mass.
    /// A larger theta value is faster but less accurate. Default value is 0.5.
    /// Out of range values are clamped (inclusive).
    /// </remarks>
    double Theta { get; }

    /// <summary>
    /// The softening factor (epsilon, ε) used to prevent numerical instability.
    /// </summary>
    /// <value>A small value greater than 0.0001, the square of which is added to the distance calculation in the gravity formula.</value>
    /// <remarks>
    /// Prevents the gravitational force from approaching infinity when two bodies get extremely close,
    /// which would otherwise lead to simulation errors and unphysically large accelerations. Default value is 0.001.
    /// Out of range values are clamped (inclusive).
    /// </remarks>
    double Epsilon { get; }

    /// <summary>
    /// The numerical integration algorithm to use for predicting a body's position and velocity. 
    /// </summary>
    IntegrationAlgorithm IntegrationAlgorithm { get; }

    /// <summary>
    /// Calculates the next position, velocity, and acceleration for a body after a given time step.
    /// This method uses the Barnes-Hut approximation to efficiently compute the net gravitational force on the body.
    /// </summary>
    /// <param name="body">The celestial body for which to evaluate the next state.</param>
    /// <param name="deltaTime">The simulation time step (in seconds) to integrate over. Can be negative to simulate backwards.</param>
    /// <param name="gridRoot">The root of the current state QuadTree, as required for the Barnes-Hut calculation.</param>
    /// <returns>
    /// A nullable record struct containing the calculated new position, velocity, and acceleration as <see cref="Vector2D"/>s.
    /// Returns <c>null</c> if the body experiences no net acceleration and its state does not need to be updated.
    /// </returns>
    EvaluationResult? EvaluateBody(ICelestialBody body, double deltaTime, IQuadTreeNode gridRoot);

    /// <summary>
    /// Atomically updates one or more properties of the Calculator.
    /// Unspecified (<c>null</c>) parameters will be ignored and their corresponding properties will remain unchanged.
    /// </summary>
    /// <param name="gravitationalConstant">The new value for the <see cref="GravitationalConstant"/> property. If null, the current value is not changed.</param>
    /// <param name="theta">The new value for the <see cref="Theta"/> property. If null, the current value is not changed.</param>
    /// <param name="epsilon">The new value for the <see cref="Epsilon"/> property. If null, the current value is not changed.</param>
    /// <param name="integrationAlgorithm">The new integration algorithm to use <see cref="IntegrationAlgorithm"/>. If null, the current value is not changed.</param>
    void Update(
        double? gravitationalConstant = null,
        double? theta = null,
        double? epsilon = null,
        IntegrationAlgorithm? integrationAlgorithm = null
    );
}

internal class Calculator : ICalculator
{
    #region Constructors

    internal Calculator(
        double gravitationalConstant = GravitationalConstant_DEFAULT,
        double theta = THETA_DEFAULT,
        double epsilon = EPSILON_DEFAULT,
        IntegrationAlgorithm algorithm = IntegrationAlgorithm_DEFAULT)
    {
        GravitationalConstant = gravitationalConstant;  // Also sets the private G
        Theta = theta;                                  // also sets private Theta_sq
        Epsilon = epsilon;                              // also sets private Epsilon_sq

        // Integration Methods must be set here as they are instance members.
        IntegrationMethods = new()
        {
            { IntegrationAlgorithm.SymplecticEuler, SymplecticEuler },
            { IntegrationAlgorithm.RungeKutta4, RungeKutta4 },
            { IntegrationAlgorithm.VelocityVerlet, VelocityVerlet }
        };
        SetIntegrationAlgorithm(algorithm); // Must be called AFTER integration methods is initialized
    }

    #endregion


    #region Fields & Properties

    public const double METERS_PER_AU = 149597870700;
    public const double SECONDS_PER_DAY = 86400;
    public const double KILOGRAM_PER_SOLAR_MASS = 1.988416e30;
    /// <summary>
    /// Conversion factor for G between SI and AC units. <c>1 au³/M☉/d²</c>(AC) = <c>0.01948746035 m³/kg/s²</c>(SI)
    /// </summary>
    public const double G_SI_PER_AC_FACTOR =
        METERS_PER_AU * METERS_PER_AU * METERS_PER_AU   // au³ => m³
        / KILOGRAM_PER_SOLAR_MASS                       // M☉ => kg
        / (SECONDS_PER_DAY * SECONDS_PER_DAY);          // d² => s²



    /// <inheritdoc/>
    public double GravitationalConstant
    {
        get;
        private set
        {
            field = value;
            G = value / G_SI_PER_AC_FACTOR;
        }
    }
    public const double GravitationalConstant_DEFAULT = 6.67430e-11;

    /// <summary>
    /// Gravitational constant in units of <c>au³/M☉/d²</c> for actual calculations. 
    /// </summary>
    private double G { get; set; }
    /// <summary>
    /// Gravitational constant in units of <c>au³/M☉/d²</c> for actual calculations. 
    /// </summary>
    public double G_AC => G;
    

    /// <inheritdoc/>
    public double Theta
    {
        get; private set
        {
            field = Math.Clamp(value, THETA_MIN, THETA_MAX);
            Theta_sq = field * field;
        }
    }
    private double Theta_sq { get; set; }
    public const double THETA_MIN = 0.0;
    public const double THETA_MAX = 1.0;
    public const double THETA_DEFAULT = 0.5;

    /// <inheritdoc/>
    public double Epsilon
    {
        get; private set
        {
            field = Math.Max(value, EPSILON_MIN);
            Epsilon_sq = field * field;
        }
    }
    private double Epsilon_sq { get; set; }
    public const double EPSILON_MIN = 0.0001;
    public const double EPSILON_DEFAULT = 0.001;
    

    private IntegrationMethod CurrentIntegrationMethod { get; set; }
    public const IntegrationAlgorithm IntegrationAlgorithm_DEFAULT = IntegrationAlgorithm.SymplecticEuler;
    public IntegrationAlgorithm IntegrationAlgorithm { get; private set; }

    private readonly Dictionary<IntegrationAlgorithm, IntegrationMethod> IntegrationMethods;

    #endregion


    /// <summary>
    /// Sets the numerical integration algorithm to use for body calculations.
    /// </summary>
    /// <param name="algorithm">The name of the algorithm as in <see cref="IntegrationAlgorithm"/></param>
    /// <exception cref="ArgumentException">If a given algorithm is not supported.</exception>
    [MemberNotNull(nameof(CurrentIntegrationMethod))]
    private void SetIntegrationAlgorithm(IntegrationAlgorithm algorithm)
    {
        if (!IntegrationMethods.TryGetValue(algorithm, out var method))
            throw new ArgumentException($"Integration algorithm '{algorithm}' is not supported.", nameof(algorithm));
        CurrentIntegrationMethod = method;
        IntegrationAlgorithm = algorithm;
    }

    /// <inheritdoc/>
    public void Update(
        double? gravitationalConstant = null,
        double? theta = null,
        double? epsilon = null,
        IntegrationAlgorithm? integrationAlgorithm = null)
    {
        if (gravitationalConstant is double g) GravitationalConstant = g;
        if (theta is double newTheta) Theta = newTheta;
        if (epsilon is double newEpsilon) Epsilon = newEpsilon;
        if (integrationAlgorithm is IntegrationAlgorithm newAlg) SetIntegrationAlgorithm(newAlg);
    }


    #region Evaluation

    /// <summary>
    /// DeltaTime cache for the current calculation step as DeltaTime is  the same for all calculations of a given tick. 
    /// Also sets <see cref="HalfDeltaTime"/>
    /// </summary>
    double DeltaTime
    {
        get; set {
            if (field == value) return;
            field = value;
            HalfDeltaTime = field / 2;
        }
    }

    double HalfDeltaTime { get; set; }

    /// <inheritdoc/>
    public EvaluationResult? EvaluateBody(ICelestialBody body, double deltaTime, IQuadTreeNode gridRoot)
    {
        DeltaTime = deltaTime;
        return CurrentIntegrationMethod.Invoke(body, gridRoot);
    }

    private delegate EvaluationResult? IntegrationMethod(ICelestialBody body, IQuadTreeNode gridRoot);

    private EvaluationResult? SymplecticEuler(ICelestialBody body, IQuadTreeNode gridRoot)
    {
        Vector2D accel = CalcAcceleration(body, gridRoot);

        if (accel.MagnitudeSquared == 0 && body.Velocity.MagnitudeSquared == 0)
            return null;    // If the body is neither moving nor experiencing any acceleration, skip calculations.

        Vector2D newVel = body.Velocity + accel * DeltaTime;
        Vector2D newPos = body.Position + newVel * DeltaTime;
        return new(newPos, newVel, accel);
    }

    /// <remarks>
    /// Dissipative and thus not time-reversable!
    /// </remarks>
    private EvaluationResult? RungeKutta4(ICelestialBody body, IQuadTreeNode gridRoot)
    {
        // K1
        Vector2D k1_pos = body.Position;
        Vector2D k1_v = body.Velocity;
        Vector2D k1_a = CalcAcceleration(body, gridRoot, k1_pos);
        if (k1_a.MagnitudeSquared == 0 && k1_v.MagnitudeSquared == 0)
            return null;    // If the body is neither moving nor experiencing any acceleration, skip calculations.

        // K2 - halfway though the time step
        Vector2D k2_pos = k1_pos + k1_v * HalfDeltaTime;
        Vector2D k2_v = k1_a * HalfDeltaTime + k1_v;
        Vector2D k2_a = CalcAcceleration(body, gridRoot, k2_pos);

        // K3 - halfway though the time step
        Vector2D k3_pos = k1_pos + k2_v * HalfDeltaTime;
        Vector2D k3_v = k2_a * HalfDeltaTime + k1_v;
        Vector2D k3_a = CalcAcceleration(body, gridRoot, k3_pos);

        // K4 - end of the time step
        Vector2D k4_pos = k1_pos + k3_v * DeltaTime;
        Vector2D k4_v = k3_a * DeltaTime + k1_v;
        Vector2D k4_a = CalcAcceleration(body, gridRoot, k4_pos);

        // Weighted average (1/6, 2/6, 2/6, 1/6)
        double sixth = 1.0 / 6.0;
        Vector2D average_v = sixth * (k1_v + 2 * k2_v + 2 * k3_v + k4_v);
        Vector2D average_a = sixth * (k1_a + 2 * k2_a + 2 * k3_a + k4_a);

        // Final weighted average rate of change over the full time step
        Vector2D newPos = k1_pos + average_v * DeltaTime;
        Vector2D newVel = k1_v + average_a * DeltaTime;

        return new(newPos, newVel, average_a);
    }

    private EvaluationResult? VelocityVerlet(ICelestialBody body, IQuadTreeNode gridRoot)
    {   
        // Step 1: Calculate the new position using current velocity and acceleration.
        // x(t + Δt) = x(t) + v(t)Δt + 0.5a(t)Δt²
        Vector2D newPos = body.Position + body.Velocity * DeltaTime + body.Acceleration * (DeltaTime * HalfDeltaTime);

        // Step 2: Calculate the new acceleration based on the NEW position.
        // a(t + Δt) = F(x(t + Δt)) / m
        Vector2D newAccel = CalcAcceleration(body, gridRoot, newPos);

        if (newAccel.MagnitudeSquared == 0 && body.Velocity.MagnitudeSquared == 0)
            return null;    // If the body is neither moving nor experiencing any acceleration, skip calculations.

        // Step 3: Calculate the new velocity using the average of the old and new acceleration.
        // v(t + Δt) = v(t) + 0.5 * (a(t) + a(t + Δt)) * Δt
        Vector2D newVel = body.Velocity + (body.Acceleration + newAccel) * HalfDeltaTime;

        return new(newPos, newVel, newAccel);
    }

    #endregion


    #region Calculation Helpers

    private Vector2D CalcAcceleration(ICelestialBody body, IQuadTreeNode node, Vector2D? probePosition = null)
    {
        // No mass = no force;
        if (node.TotalMass == 0) return Vector2D.Zero;

        // Check early if the node only contains itself
        if (node.IsExternal && node.Count == 1 && node.Bodies.First() == body) return Vector2D.Zero;

        var measuringPos = probePosition ?? body.Position;

        double s_sq = node.Boundary.MaxDimension * node.Boundary.MaxDimension;
        double d_sq = measuringPos.DistanceToSquared(node.CenterOfMass);
        bool isFar = s_sq / d_sq < Theta_sq;

        // Case: Node is far away => simply treat the node as a single point mass
        if (isFar) return CalcAccelFromPoint(measuringPos, node.CenterOfMass, node.TotalMass, d_sq);

        // Case: Node is Internal => Recurse into children and aggregate the results
        if (!node.IsExternal) return AggregateAccel(body, node.Children, measuringPos);

        // Case: Node is External => Calculate for every body in the node other than itself and aggregate the results
        return AggregateAccel(body, node.Bodies, measuringPos);
    }

    private Vector2D AggregateAccel(ICelestialBody body, IEnumerable<IQuadTreeNode> children, Vector2D probePosition)
    {
        var accel = Vector2D.Zero;
        foreach (var child in children) accel += CalcAcceleration(body, child, probePosition);
        return accel;
    }

    private Vector2D AggregateAccel(ICelestialBody body, IEnumerable<ICelestialBody> bodies, Vector2D probePosition)
    {
        var accel = Vector2D.Zero;
        foreach (var otherBody in bodies)
        {
            if (otherBody != body) accel += CalcAccelFromPoint(probePosition, otherBody.Position, otherBody.Mass);
        }
        return accel;
    }

    /// <summary>
    /// Calculates the acceleration exerted on a point mass from a source mass.
    /// </summary>
    /// <param name="targetPos">The position of the object being accelerated.</param>
    /// <param name="sourcePos">The position of the mass causing the acceleration.</param>
    /// <param name="sourceMass">The mass of the source.</param>
    /// <param name="distanceSquared">Optional pre-calculated squared distance to avoid a square root.</param>
    /// <returns>The acceleration vector.</returns>
    private Vector2D CalcAccelFromPoint(Vector2D targetPos, Vector2D sourcePos, double sourceMass, double? distanceSquared = null)
    {
        double d_sq = distanceSquared ?? targetPos.DistanceToSquared(sourcePos);
        if (d_sq == 0) return Vector2D.Zero;

        // F = G * m1 * m2 / (d^2 + e^2)
        // a = F / m1 = G * m2 / (d^2 + e^2)
        double accelerationMagnitude = G * sourceMass / (d_sq + Epsilon_sq);

        // Vector from target to source, then normalize to get direction
        Vector2D direction = (sourcePos - targetPos).Normalized;

        return direction * accelerationMagnitude;
    }

    #endregion
}
