using System.Diagnostics.CodeAnalysis;
using Physics.Bodies;
using Physics.Models;

namespace Physics.Core;

internal interface ICalculator
{
    /// <summary>
    /// The value for the gravitational constant G in cubic meters per kilogram per second squared.
    /// </summary>
    /// <value>The gravitational constant, in units of m^3 kg^-1 s^-2.</value>
    double G { get; }

    /// <summary>
    /// The opening-angle parameter (theta, θ) for the Barnes-Hut algorithm.
    /// </summary>
    /// <value>A value, between 0 and 1, that controls the trade-off between accuracy and computational speed.</value>
    /// <remarks>
    /// A smaller theta value results in higher accuracy but more calculations, as tree nodes must be closer to be treated as a single mass.
    /// A larger theta value is faster but less accurate. A common value is 0.5.
    /// </remarks>
    double Theta { get; }

    /// <summary>
    /// The softening factor (epsilon, ε) used to prevent numerical instability.
    /// </summary>
    /// <value>A small, non-zero value added to the distance calculation in the gravity formula.</value>
    /// <remarks>
    /// Prevents the gravitational force from approaching infinity when two bodies get extremely close,
    /// which would otherwise lead to simulation errors and unphysically large accelerations.
    /// </remarks>
    double Epsilon { get; }

    /// <summary>
    /// The numerical integration algorithm to use for predicting a body's position and velocity. 
    /// </summary>
    IntegrationAlgorithm IntegrationAlgorithm { get; }

    /// <summary>
    /// Calculates the next position and velocity for a body after a given time step.
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
    /// Atomically updates one or more properties of the timer.
    /// Unspecified (<c>null</c>) parameters will be ignored and their corresponding properties will remain unchanged.
    /// </summary>
    /// <param name="gravitationalConstant">The new value for the <see cref="G"/> property. If null, the current value is not changed.</param>
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
        double gravitationalConstant = 6.67430e-11,
        double theta = 0.5,
        double epsilon = 0.001,
        IntegrationAlgorithm algorithm = IntegrationAlgorithm.SymplecticEuler)
    {
        G = gravitationalConstant;
        Theta = theta;
        Epsilon = epsilon;
        SetIntegrationAlgorithm(algorithm);

        // Pre-Square
        Theta_sq = Theta * Theta;
        Epsilon_sq = epsilon * epsilon;

        // Integration Methods must be set here as they are instance members.
        IntegrationMethods = new()
        {
            { IntegrationAlgorithm.SymplecticEuler, SymplecticEuler },
            { IntegrationAlgorithm.RungeKutta4, RungeKutta4 },
            { IntegrationAlgorithm.VelocityVerlet, VelocityVerlet }
        };
    }

    #endregion


    #region Fields & Properties

    /// <inheritdoc/>
    public double G { get; private set; }
    /// <inheritdoc/>
    public double Theta { get; private set { field = Math.Clamp(value, 0, 1); } }
    /// <inheritdoc/>
    public double Epsilon { get; private set { field = Math.Max(value, 0.0001); } }

    private double Epsilon_sq { get; set; }
    private double Theta_sq { get; set; }

    private IntegrationMethod _currentIntegrationMethod { get; set; }
    public IntegrationAlgorithm IntegrationAlgorithm { get; private set; }

    private readonly Dictionary<IntegrationAlgorithm, IntegrationMethod> IntegrationMethods;

    #endregion


    /// <summary>
    /// Sets the numerical integration algorithm to use for body calculations.
    /// </summary>
    /// <param name="algorithm">The name of the algorithm as in <see cref="IntegrationAlgorithm"/></param>
    /// <exception cref="ArgumentException">If a given algorithm is not supported.</exception>
    [MemberNotNull(nameof(_currentIntegrationMethod))]
    private void SetIntegrationAlgorithm(IntegrationAlgorithm algorithm)
    {
        if (!IntegrationMethods.TryGetValue(algorithm, out var method))
            throw new ArgumentException($"Integration algorithm '{algorithm}' is not supported.", nameof(algorithm));
        _currentIntegrationMethod = method;
        IntegrationAlgorithm = algorithm;
    }

    /// <inheritdoc/>
    public void Update(
        double? gravitationalConstant = null,
        double? theta = null,
        double? epsilon = null,
        IntegrationAlgorithm? integrationAlgorithm = null)
    {
        if (gravitationalConstant is double g) G = g;
        if (theta is double newTheta)
        {
            Theta = newTheta;
            Theta_sq = newTheta * newTheta;
        }
        if (epsilon is double newEpsilon)
        {
            Epsilon = newEpsilon;
            Epsilon_sq = newEpsilon * newEpsilon;
        }
        if (integrationAlgorithm is IntegrationAlgorithm newAlg) SetIntegrationAlgorithm(newAlg);
    }


    #region Evaluation

    /// <inheritdoc/>
    public EvaluationResult? EvaluateBody(ICelestialBody body, double deltaTime, IQuadTreeNode gridRoot)
        => _currentIntegrationMethod.Invoke(body, deltaTime, gridRoot);


    private delegate EvaluationResult? IntegrationMethod(
        ICelestialBody body, double deltaTime, IQuadTreeNode gridRoot);

    private EvaluationResult? SymplecticEuler(ICelestialBody body, double deltaTime, IQuadTreeNode gridRoot)
    {
        Vector2D accel = CalcAcceleration(body, gridRoot);

        if (accel.MagnitudeSquared == 0 && body.Velocity.MagnitudeSquared == 0)
            return null;    // If the body is neither moving nor experiencing any acceleration, skip calculations.

        Vector2D newVel = body.Velocity + accel * deltaTime;
        Vector2D newPos = body.Position + newVel * deltaTime;
        return new(newPos, newVel, accel);
    }

    private EvaluationResult? RungeKutta4(ICelestialBody body, double deltaTime, IQuadTreeNode gridRoot)
    {
        // K1
        Vector2D k1_pos = body.Position;
        Vector2D k1_v = body.Velocity;
        Vector2D k1_a = CalcAcceleration(body, gridRoot, k1_pos);
        if (k1_a.MagnitudeSquared == 0 && k1_v.MagnitudeSquared == 0)
            return null;    // If the body is neither moving nor experiencing any acceleration, skip calculations.

        double halfDeltaTime = deltaTime / 2;

        // K2 - halfway though the time step
        Vector2D k2_pos = k1_pos + k1_v * halfDeltaTime;
        Vector2D k2_v = k1_a * halfDeltaTime + k1_v;
        Vector2D k2_a = CalcAcceleration(body, gridRoot, k2_pos);

        // K3 - halfway though the time step
        Vector2D k3_pos = k1_pos + k2_v * halfDeltaTime;
        Vector2D k3_v = k2_a * halfDeltaTime + k1_v;
        Vector2D k3_a = CalcAcceleration(body, gridRoot, k3_pos);

        // K4 - end of the time step
        Vector2D k4_pos = k1_pos + k3_v * deltaTime;
        Vector2D k4_v = k3_a * deltaTime + k1_v;
        Vector2D k4_a = CalcAcceleration(body, gridRoot, k4_pos);

        // Weighted average (1/6, 2/6, 2/6, 1/6)
        double sixth = 1.0 / 6.0;
        Vector2D average_v = sixth * (k1_v + 2 * k2_v + 2 * k3_v + k4_v);
        Vector2D average_a = sixth * (k1_a + 2 * k2_a + 2 * k3_a + k4_a);

        // Final weighted average rate of change over the full time step
        Vector2D newPos = k1_pos + average_v * deltaTime;
        Vector2D newVel = k1_v + average_a * deltaTime;

        return new(newPos, newVel, average_a);
    }

    private EvaluationResult? VelocityVerlet(ICelestialBody body, double deltaTime, IQuadTreeNode gridRoot)
    {
        Vector2D newAccel = CalcAcceleration(body, gridRoot);
        if (newAccel.MagnitudeSquared == 0 && body.Velocity.MagnitudeSquared == 0)
            return null;    // If the body is neither moving nor experiencing any acceleration, skip calculations.

        double halfDeltaTime = deltaTime / 2;

        // r(t+Δt) = r(t) + v(t)*dt + 0.5*a(t)*dt^2
        Vector2D newPos = body.Position
            + (body.Velocity * deltaTime)
            + (body.Acceleration * deltaTime * halfDeltaTime);

        // v(t+Δt) = v(t) + 0.5*(a(t) + a(t+dt)) * dt
        Vector2D newVel = body.Velocity
            + (body.Acceleration + newAccel) * halfDeltaTime;

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
        double forceMag = G * sourceMass / (d_sq + Epsilon_sq);

        // Vector from target to source, then normalize to get direction
        Vector2D direction = (sourcePos - targetPos).Normalized;

        return direction * forceMag;
    }

    #endregion
}
