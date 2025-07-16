using Physics.Bodies;

namespace Physics.Models;

/// <summary>
/// Axis-Aligned Bounding Box.<br/>
/// Represents a rectangular area in 2D space,
/// defined by a center point and its half-dimensions (half-width and half-height).
/// </summary>
internal readonly struct AABB
{
    internal AABB(Vector2D center, Vector2D halfDimension)
    {
        Center = center;
        HalfDimension = new(Math.Abs(halfDimension.X), Math.Abs(halfDimension.Y));  // Ensure positive HalfDimension
        Width = HalfDimension.X * 2;
        Height = HalfDimension.Y * 2;
        MaxDimension = Math.Max(Width, Height);
    }

    /// <summary>
    /// The geometric center of the bounding rectangle.
    /// </summary>
    internal Vector2D Center { get; }

    /// <summary>
    /// A vector representing half the width and half the height of the box.
    /// This is used for efficient calculation of the box's corners.
    /// </summary>
    internal Vector2D HalfDimension { get; }

    /// <summary>
    /// The full width of the bounding box.
    /// </summary>
    internal double Width { get; }

    /// <summary>
    /// The full height of the bounding box.
    /// </summary>
    internal double Height { get; }

    /// <summary>
    /// The largest dimension (width or height) of the bounding box.
    /// </summary>
    internal double MaxDimension { get; }

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
    internal bool Contains(CelestialBody body) => Contains(body.Position);

    /// <summary>
    /// Checks if a point's position is containd within this bounding box.
    /// The check is inclusive of the minimum boundary and exclusive of the maximum boundary
    /// [min, max) to ensure points on an edge belong to only one quadrant.
    /// </summary>
    /// <param name="point">The point to check.</param>
    /// <returns>True if the point is inside the boundary, false otherwise.</returns>
    internal bool Contains(Vector2D point)
    {
        var min = Min;
        var max = Max;
        return point.X >= min.X && point.X < max.X &&
                point.Y >= min.Y && point.Y < max.Y;
    }
}
