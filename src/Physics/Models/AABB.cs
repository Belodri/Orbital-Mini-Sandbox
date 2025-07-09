using Physics.Bodies;

namespace Physics.Models;

/// <summary>
/// Axis-Aligned Bounding Box.<br/>
/// Represents a rectangular area in 2D space,
/// defined by a center point and its half-dimensions (half-width and half-height).
/// </summary>
internal readonly struct AABB(Vector2D center, Vector2D halfDimension)
{
    /// <summary>
    /// The geometric center of the bounding rectangle.
    /// </summary>
    internal Vector2D Center { get; } = center;

    /// <summary>
    /// A vector representing half the width and half the height of the box.
    /// This is used for efficient calculation of the box's corners.
    /// </summary>
    internal Vector2D HalfDimension { get; } = new(Math.Abs(halfDimension.X), Math.Abs(halfDimension.Y));   // Ensure positive HalfDimension

    /// <summary>
    /// The minimum corner of the box (bottom-left).
    /// </summary>
    internal Vector2D Min => Center - HalfDimension;

    /// <summary>
    /// The maximum corner of the box (top-right).
    /// </summary>
    internal Vector2D Max => Center + HalfDimension;

    /// <summary>
    /// Checks if a celestial body's position is contained within this bounding box.
    /// The check is inclusive of the minimum boundary and exclusive of the maximum boundary
    /// [min, max) to ensure bodies on an edge belong to only one quadrant.
    /// </summary>
    /// <param name="body">The celestial body to check.</param>
    /// <returns>True if the body is inside the boundary, otherwise false.</returns>
    internal bool Contains(CelestialBody body)
    {
        var pos = body.Position;
        var min = Min;
        var max = Max;
        return pos.X >= min.X && pos.X < max.X &&
                pos.Y >= min.Y && pos.Y < max.Y;
    }
}
