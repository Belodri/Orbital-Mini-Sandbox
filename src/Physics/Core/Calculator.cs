using Physics.Bodies;
using Physics.Models;

namespace Physics.Core;

internal class Calculator
{
    #region Constructors

    internal Calculator() : this(DEFAULT_DATA) { }

    internal Calculator(CalculatorData data)
    {
        G = data.GravitationalConstant;
        // Pre-Square
        Theta_sq = data.Theta * data.Theta;
        SofteningFactor_sq = data.SofteningFactor * data.SofteningFactor;
    }

    internal static readonly CalculatorData DEFAULT_DATA = new(0.5, 6.67430e-11, 0.001);

    #endregion


    #region Fields & Properties

    private readonly double G;
    private readonly double SofteningFactor_sq;
    private readonly double Theta_sq;

    #endregion


    #region Body Evaluations

    /// <summary>
    /// Evaluates the new position and velocity of a body.
    /// </summary>
    /// <param name="body">The body for which to evaluate the next position and velocity.</param>
    /// <param name="deltaTime">Time difference in time (in ms) from the current to the next state. Negative to go backwards in time.</param>
    /// <param name="gridRoot">The root of the QuadTree reflecting the current state of all bodies.</param>
    /// <returns>Update data DTO to update the body to the next state.</returns>
    internal BodyDataPartial? EvaluateBody(CelestialBody body, double deltaTime, QuadTreeNode gridRoot)
    {
        Vector2D acc = CalcAcceleration(body, gridRoot);
        if (acc.MagnitudeSquared == 0) return null;

        // Simple Euler-Cromer integration for now
        Vector2D newVel = body.Velocity + acc * deltaTime;
        Vector2D newPos = body.Position + newVel * deltaTime;

        return new(body.Id, null, null, newPos.X, newPos.Y, newVel.X, newVel.Y);
    }

    private Vector2D CalcAcceleration(CelestialBody body, QuadTreeNode node)
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

    private Vector2D AggregateAccel(CelestialBody body, IEnumerable<QuadTreeNode> children)
    {
        var accel = Vector2D.Zero;
        foreach (var child in children) accel += CalcAcceleration(body, child);
        return accel;
    }

    private Vector2D AggregateAccel(CelestialBody body, IEnumerable<CelestialBody> bodies)
    {
        var accel = Vector2D.Zero;
        foreach (var otherBody in bodies)
        {
            if (otherBody != body) accel += CalcAccelFromPoint(body, otherBody.Position, otherBody.Mass);
        }
        return accel;
    }

    private Vector2D CalcAccelFromPoint(CelestialBody targetBody, Vector2D sourcePos, double sourceMass, double? distanceSquared = null)
    {
        double d_sq = distanceSquared ?? targetBody.Position.DistanceToSquared(sourcePos);
        if (d_sq == 0) return Vector2D.Zero;

        // F = G * m1 * m2 / (d^2 + e)
        // a = F / m1 = G * m2 / (d^2 + e)
        double forceMag = G * sourceMass / (d_sq + SofteningFactor_sq);

        // Vector from target to source, then normalize to get direction
        Vector2D direction = (sourcePos - targetBody.Position).Normalized;

        return direction * forceMag;
    }
    
    #endregion
}
