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
    /// Calculates the next position and velocity for a body after a given time step.
    /// This method uses the Barnes-Hut approximation to efficiently compute the net gravitational force on the body.
    /// </summary>
    /// <param name="body">The celestial body for which to evaluate the next state.</param>
    /// <param name="deltaTime">The simulation time step (in seconds) to integrate over. Can be negative to simulate backwards.</param>
    /// <param name="gridRoot">The root of the current state QuadTree, as required for the Barnes-Hut calculation.</param>
    /// <returns>
    /// A nullable tuple containing the calculated new position and new velocity as <see cref="Vector2D"/>s.
    /// Returns <c>null</c> if the body experiences no net acceleration and its state does not need to be updated.
    /// </returns>
    (Vector2D newPosition, Vector2D newVelocity)? EvaluateBody(ICelestialBody body, double deltaTime, IQuadTreeNode gridRoot);

    /// <summary>
    /// Atomically updates one or more properties of the timer.
    /// Unspecified (<c>null</c>) parameters will be ignored and their corresponding properties will remain unchanged.
    /// </summary>
    /// <param name="gravitationalConstant">The new value for the <see cref="G"/> property. If null, the current value is not changed.</param>
    /// <param name="theta">The new value for the <see cref="Theta"/> property. If null, the current value is not changed.</param>
    /// <param name="epsilon">The new X component for the <see cref="Epsilon"/> vector. If null, the X component is not changed.</param>
    void Update(
        double? gravitationalConstant = null,
        double? theta = null,
        double? epsilon = null
    );
}

internal class Calculator : ICalculator
{
    #region Constructors

    internal Calculator(
        double gravitationalConstant = 6.67430e-11,
        double theta = 0.5,
        double epsilon = 0.001)
    {
        G = gravitationalConstant;
        Theta = theta;
        Epsilon = epsilon;

        // Pre-Square
        Theta_sq = Theta * Theta;
        Epsilon_sq = epsilon * epsilon;
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

    #endregion

    /// <inheritdoc/>
    public void Update(
        double? gravitationalConstant = null,
        double? theta = null,
        double? epsilon = null)
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
    }

    #region Body Evaluations

    /// <inheritdoc/>
    public (Vector2D newPosition, Vector2D newVelocity)? EvaluateBody(ICelestialBody body, double deltaTime, IQuadTreeNode gridRoot)
    {
        Vector2D acc = CalcAcceleration(body, gridRoot);
        if (acc.MagnitudeSquared == 0) return null;

        // Simple Euler-Cromer integration for now
        Vector2D newVel = body.Velocity + acc * deltaTime;
        Vector2D newPos = body.Position + newVel * deltaTime;

        return (newPos, newVel);
    }

    private Vector2D CalcAcceleration(ICelestialBody body, IQuadTreeNode node)
    {
        // No mass = no force;
        if (node.TotalMass == 0) return Vector2D.Zero;

        // Check early if the node only contains itself
        if (node.IsExternal && node.Count == 1 && node.Bodies.First() == body) return Vector2D.Zero;

        double s_sq = node.Boundary.MaxDimension * node.Boundary.MaxDimension;
        double d_sq = body.Position.DistanceToSquared(node.CenterOfMass);
        bool isFar = s_sq / d_sq < Theta_sq;

        // Case: Node is far away => simply treat the node as a single point mass
        if (isFar) return CalcAccelFromPoint(body, node.CenterOfMass, node.TotalMass, d_sq);

        // Case: Node is Internal => Recurse into children and aggregate the results
        if (!node.IsExternal) return AggregateAccel(body, node.Children);

        // Case: Node is External => Calculate for every body in the node other than itself and aggregate the results
        return AggregateAccel(body, node.Bodies);
    }

    private Vector2D AggregateAccel(ICelestialBody body, IEnumerable<IQuadTreeNode> children)
    {
        var accel = Vector2D.Zero;
        foreach (var child in children) accel += CalcAcceleration(body, child);
        return accel;
    }

    private Vector2D AggregateAccel(ICelestialBody body, IEnumerable<ICelestialBody> bodies)
    {
        var accel = Vector2D.Zero;
        foreach (var otherBody in bodies)
        {
            if (otherBody != body) accel += CalcAccelFromPoint(body, otherBody.Position, otherBody.Mass);
        }
        return accel;
    }

    private Vector2D CalcAccelFromPoint(ICelestialBody targetBody, Vector2D sourcePos, double sourceMass, double? distanceSquared = null)
    {
        double d_sq = distanceSquared ?? targetBody.Position.DistanceToSquared(sourcePos);
        if (d_sq == 0) return Vector2D.Zero;

        // F = G * m1 * m2 / (d^2 + e)
        // a = F / m1 = G * m2 / (d^2 + e)
        double forceMag = G * sourceMass / (d_sq + Epsilon_sq);

        // Vector from target to source, then normalize to get direction
        Vector2D direction = (sourcePos - targetBody.Position).Normalized;

        return direction * forceMag;
    }

    #endregion
}
