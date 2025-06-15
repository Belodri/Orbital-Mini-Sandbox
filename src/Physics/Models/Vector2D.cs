// Implement after performance tests if necessary:
// using System.Runtime.CompilerServices;
// [MethodImpl(MethodImplOptions.AggressiveInlining)]

namespace Physics.Models;

public readonly struct Vector2D(double x, double y) : IEquatable<Vector2D>
{
    public double X { get; } = x;
    public double Y { get; } = y;

    #region Static Properties

    public static readonly Vector2D Zero = new(0, 0);
    public static readonly Vector2D One = new(1, 1);
    public static readonly Vector2D UnitX = new(1, 0);
    public static readonly Vector2D UnitY = new(0, 1);

    #endregion

    #region Properties

    public double Magnitude => Math.Sqrt(MagnitudeSquared);

    public double MagnitudeSquared => X * X + Y * Y;

    public Vector2D Normalized
    {
        get
        {
            double magSq = MagnitudeSquared;    
            if (magSq > 0)
            {
                // Small performance optimization. Same as
                // new(X / Magnitude, Y / Magnitude)
                double invMag = 1.0 / Math.Sqrt(magSq);
                return new(X * invMag, Y * invMag);
            }
            return Zero;
        }
    }

    /// <summary>
    /// Gets a vector that is perpendicular (rotated 90 degrees counter-clockwise) to this vector.
    /// </summary>
    public Vector2D Perpendicular => new(-Y, X);

    /// <summary>
    /// Gets the angle of the vector in radians, measured counter-clockwise from the positive X-axis.
    /// </summary>
    public double Angle => Math.Atan2(Y, X);

    #endregion


    #region Vector Operations

    public double Dot(Vector2D other) => X * other.X + Y * other.Y;

    /// <summary>
    /// Calculates the distance between this point and another.
    /// </summary>
    /// <returns>Euclidean distance</returns>
    public double DistanceTo(Vector2D other)
    {
        double dx = X - other.X;
        double dy = Y - other.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>
    /// Calculates the squared distance between this point and another.
    /// </summary>
    /// <returns>Squared Euclidean distance</returns>
    public double DistanceToSquared(Vector2D other)
    {
        double dx = X - other.X;
        double dy = Y - other.Y;
        return dx * dx + dy * dy;
    }
    #endregion


    #region Utility

    public static Vector2D Lerp(Vector2D a, Vector2D b, double t)
    {
        return new(
            a.X + (b.X - a.X) * t,
            a.Y + (b.Y - a.Y) * t
        );
    }

    /// <summary>
    /// Returns a copy of the vector with its magnitude clamped to a maximum value.
    /// </summary>
    /// <param name="maxMagnitude">The maximum allowed magnitude.</param>
    /// <returns>A new vector with a magnitude no greater than <paramref name="maxMagnitude"/>.</returns>
    public Vector2D ClampMagnitude(double maxMagnitude)
    {
        if (maxMagnitude < 0)
            throw new ArgumentOutOfRangeException(nameof(maxMagnitude), "Maximum magnitude cannot be negative.");

        double magSq = MagnitudeSquared;
        if (magSq > maxMagnitude * maxMagnitude)
        {
            // Small performance optimization. Same as
            // double mag = Math.Sqrt(magSq);
            // new(X / mag * maxMagnitude, Y / mag * maxMagnitude);
            double scale = maxMagnitude / Math.Sqrt(magSq);
            return new(X * scale, Y * scale);
        }
        return this;
    }

    public void Deconstruct(out double x, out double y)
    {
        x = X;
        y = Y;
    }

    #endregion


    #region Arithmetic Operators

    public static Vector2D operator +(Vector2D a, Vector2D b) => new(a.X + b.X, a.Y + b.Y);
    public static Vector2D operator -(Vector2D a, Vector2D b) => new(a.X - b.X, a.Y - b.Y);
    public static Vector2D operator -(Vector2D v) => new(-v.X, -v.Y);
    public static Vector2D operator *(Vector2D v, double scalar) => new(v.X * scalar, v.Y * scalar);
    public static Vector2D operator *(double scalar, Vector2D v) => new(v.X * scalar, v.Y * scalar);
    public static Vector2D operator /(Vector2D v, double scalar) => new(v.X / scalar, v.Y / scalar);

    #endregion


    #region Factory Methods

    /// <summary>
    /// Creates a new vector from a given angle and magnitude.
    /// </summary>
    /// <param name="angle">The angle in radians from the positive X-axis.</param>
    /// <param name="magnitude">The desired length of the vector.</param>
    /// <returns>The new <see cref="Vector2D"/>.</returns>
    public static Vector2D FromAngle(double angle, double magnitude = 1.0)
        => new(Math.Cos(angle) * magnitude, Math.Sin(angle) * magnitude);

    #endregion


    #region String Representation

    public override string ToString() => $"({X:F6}, {Y:F6})";

    public string ToString(string format) => $"({X.ToString(format)}, {Y.ToString(format)})";

    #endregion


    #region Comparisons

    /// <summary>
    /// Determines if two vectors are exactly equal. For calculations involving floating-point
    /// arithmetic, consider using <see cref="ApproximatelyEquals"/> instead.
    /// </summary>
    public static bool operator ==(Vector2D a, Vector2D b) => a.X == b.X && a.Y == b.Y;

    public static bool operator !=(Vector2D a, Vector2D b) => !(a == b);

    public bool Equals(Vector2D other) => X == other.X && Y == other.Y;

    public override bool Equals(object? obj) => obj is Vector2D other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(X, Y);

    /// <summary>
    /// Compares two vectors for approximate equality using a tolerance.
    /// </summary>
    /// <param name="other">The vector to compare against.</param>
    /// <param name="tolerance">The maximum allowed difference for each component.</param>
    /// <returns><c>true</c> if the vectors are approximately equal; otherwise, <c>false</c>.</returns>
    public bool ApproximatelyEquals(Vector2D other, double tolerance = 1e-10)
    {
        return Math.Abs(X - other.X) < tolerance && Math.Abs(Y - other.Y) < tolerance;
    }

    #endregion
}