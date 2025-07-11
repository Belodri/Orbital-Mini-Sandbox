using Physics.Bodies;
using Physics.Models;

namespace Physics.Core;

/// <summary>
/// Manages the QuadTree spatial partitioning structure for the simulation.
/// This class acts as a facade, simplifying the process of building and
/// accessing the quadtree. It is responsible for creating a new tree from a
/// list of celestial bodies on each simulation tick.
/// </summary>
internal class Grid
{
    internal static readonly double PADDING_MULT = 0.01;
    internal static readonly double PADDING_FLAT = 1e-10;

    /// <summary>
    /// The root node of the quadtree. This is the entry point for any
    /// operation that needs to traverse the tree, such as the force
    /// calculations performed by the Calculator.
    /// Nullable because the grid is empty until the Rebuild method is called.
    /// </summary>
    internal QuadTreeNode? Root { get; private set; }

    /// <summary>
    /// The all-encompassing AABB that contains every body in the simulation
    /// for the current tick. This is calculated at the start of the Rebuild
    /// process and used to define the boundary of the Root node.
    /// </summary>
    internal AABB OuterBounds { get; private set; }

    /// <summary>
    /// Discards the old quadtree and constructs a new one based on the current
    /// positions of provided celestial bodies.
    /// Assumes that filtering out disabled bodies has been done beforehand!
    /// </summary>
    /// <param name="enabledBodies">A list of bodies in the simulation.</param>
    internal void Rebuild(IReadOnlyList<CelestialBody> enabledBodies)
    {
        OuterBounds = CalculateOuterBounds(enabledBodies);
        Root = null;

        if (enabledBodies.Count == 0) return;

        Root = new(OuterBounds);
        foreach (var body in enabledBodies) Root.Insert(body);
        Root.Evaluate();
    }

    /// <summary>
    /// Calculates a bounding box that encloses all provided celestial bodies.
    /// A small padding is added to ensure bodies on the edge are fully contained.
    /// </summary>
    /// <param name="bodies">A list of bodies to bound.</param>
    /// <returns>An AABB that encloses all bodies.</returns>
    static AABB CalculateOuterBounds(IReadOnlyList<CelestialBody> bodies)
    {
        if (bodies.Count == 0) return new(Vector2D.Zero, Vector2D.Zero);

        var p0 = bodies[0].Position;
        double minX = p0.X;
        double minY = p0.Y;
        double maxX = p0.X;
        double maxY = p0.Y;

        for (int i = 1; i < bodies.Count; i++)
        {
            var (x, y) = bodies[i].Position;
            minX = Math.Min(minX, x);
            minY = Math.Min(minY, y);
            maxX = Math.Max(maxX, x);
            maxY = Math.Max(maxY, y);
        }

        double width = maxX - minX;
        double height = maxY - minY;
        double halfWidth = width / 2.0;
        double halfHeight = height / 2.0;

        // Combination of relative and absolute padding to ensure bodies
        // on the exclusive max boundary are inside.
        double padding = Math.Max(width, height) * PADDING_MULT + PADDING_FLAT;
        Vector2D center = new(minX + halfWidth, minY + halfHeight);
        Vector2D halfDimension = new(halfWidth + padding, halfHeight + padding);

        return new(center, halfDimension);
    }
}