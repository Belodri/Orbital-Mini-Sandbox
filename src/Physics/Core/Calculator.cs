using Physics.Models;

namespace Physics.Core;

internal interface ICalculator
{
    /// <summary>
    /// The gravitational constant, in units of m³/kg/s².
    /// </summary>
    double G_SI { get; }
    /// <summary>
    /// Gravitational constant in units of <c>au³/M☉/d²</c>.
    /// </summary>
    double G_AC { get; }
    /// <summary>
    /// Alias for <see cref="G_AC"/>
    /// </summary>
    public double G => G_AC;
    /// <summary>
    /// The opening-angle parameter (theta, θ) for the Barnes-Hut algorithm.
    /// </summary>
    /// <value>A value, between 0 and 1 (inclusive), that controls the trade-off between accuracy and computational speed.</value>
    /// <remarks>
    /// A smaller theta value results in higher accuracy but more calculations, as tree nodes must be closer to be treated as a single mass.
    /// A larger theta value is faster but less accurate. 
    /// <br/>Default value is 0.5.
    /// <br/>Out of range values throw an error.
    /// </remarks>
    double Theta { get; }
    double ThetaSquared { get; }
    /// <summary>
    /// The softening factor (epsilon, ε) used to prevent numerical instability.
    /// </summary>
    /// <value>A small value greater than 0.0001, the square of which is added to the distance calculation in the gravity formula.</value>
    /// <remarks>
    /// Prevents the gravitational force from approaching infinity when two bodies get extremely close,
    /// which would otherwise lead to simulation errors and unphysically large accelerations. 
    /// <br/>Default value is 0.001.
    /// <br/>Out of range values throw an error.
    /// </remarks>
    double Epsilon { get; }
    double EpsilonSquared { get; }
    /// <summary>
    /// Atomically updates one or more properties of the Calculator.
    /// Unspecified or null parameters will remain unchanged.
    /// </summary>
    /// <param name="g_SI">The new value for the <see cref="G_SI"/> property.</param>
    /// <param name="theta">The new value for the <see cref="Theta"/> property.</param>
    /// <param name="epsilon">The new value for the <see cref="Epsilon"/> property.</param>
    /// <remarks>
    /// Altering the properties of a running simulation breaks time-reversability!
    /// </remarks> 
    void Update(double? g_SI, double? theta, double? epsilon);
    /// <summary>
    /// Calculates the acceleration an object m1 experiences from a mass m2. 
    /// </summary>
    /// <param name="m1Position">The position of the object being accelerated.</param>
    /// <param name="m2Position">The position of the object exerting the force.</param>
    /// <param name="m2Mass">The mass of the object exerting the force.</param>
    /// <param name="distanceSquaredSoftened">If the softened square of distance has alrady been calculated, it can be passed here to avoid recalculation.</param>
    /// <returns>The acceleration vector of the object being accelerated.</returns>
    Vector2D Acceleration(Vector2D m1Position, Vector2D m2Position, double m2Mass, double? distanceSquaredSoftened = null);
    /// <summary>
    /// Calculates the squared distance between two points and adds a softening factor ε.
    /// This softened distance can never be zero!
    /// </summary>
    double DistanceSquaredSoftened(Vector2D pointA, Vector2D pointB);
}

internal class Calculator : ICalculator
{
    public Calculator(double g_SI = G_SI_DEFAULT, double theta = THETA_DEFAULT, double epsilon = EPSILON_DEFAULT)
        => Update(g_SI, theta, epsilon);

    #region Constants

    public const double G_SI_DEFAULT = 6.67430e-11;

    public const double THETA_MIN = 0.0;
    public const double THETA_MAX = 1.0;
    public const double THETA_DEFAULT = 0.5;

    public const double EPSILON_MIN = 0.0001;
    public const double EPSILON_DEFAULT = 0.001;

    public const double MIN_DISTANCE_SQUARED = 1e-12;

    #region Unit Conversions 

    public const double METERS_PER_AU = 149597870700;
    public const double SECONDS_PER_DAY = 86400;
    public const double KILOGRAM_PER_SOLAR_MASS = 1.988416e30;

    /*
        Unit conversion m³/kg/s² => au³/M☉/d²

        x [au³ * M☉⁻¹ * d⁻²] = 1 [m³ * kg⁻¹ * s⁻²]
        x = [au⁻³ * M☉ * d²] * [m³ * kg⁻¹ * s⁻²]
        x = METERS_PER_AU⁻³ [m⁻³] * KILOGRAM_PER_SOLAR_MASS [kg] * SECONDS_PER_DAY² [s²] * 1 [m³] * 1 [kg⁻¹] * 1 [s⁻²]
        x = METERS_PER_AU⁻³ * KILOGRAM_PER_SOLAR_MASS * SECONDS_PER_DAY²
    */

    /// <summary>
    /// Conversion factor for G from SI to AC units. <c>1 m³/kg/s²</c>(SI) = <c>4.43362031e6 au³/M☉/d²</c>(AC)
    /// </summary>
    public const double G_AC_PER_SI_FACTOR =
        1 / (METERS_PER_AU * METERS_PER_AU * METERS_PER_AU)
        * KILOGRAM_PER_SOLAR_MASS
        * SECONDS_PER_DAY * SECONDS_PER_DAY;

    /// <summary>
    /// Conversion factor for G from AC to SI units. <c>1 au³/M☉/d²</c>(AC) = <c>2.2554931e-7 m³/kg/s²</c>(SI)
    /// </summary>
    public const double G_SI_PER_AC_FACTOR = 1 / G_AC_PER_SI_FACTOR;

    #endregion

    /// <inheritdoc/>
    public double G_SI { get; private set; }
    /// <inheritdoc/>
    public double G_AC { get; private set; }
    /// <summary>
    /// Alias for <see cref="G_AC"/>
    /// </summary>
    public double G => G_AC;

    /// <inheritdoc/>
    public double Theta { get; private set; }
    public double ThetaSquared { get; private set; }

    /// <inheritdoc/>
    public double Epsilon { get; private set; }
    public double EpsilonSquared { get; private set; }

    #endregion

    /// <inheritdoc/>
    public void Update(
        double? g_SI = null,
        double? theta = null,
        double? epsilon = null
    )
    {
        if (g_SI is double newG_SI)
        {
            G_SI = newG_SI;
            G_AC = newG_SI / G_SI_PER_AC_FACTOR;
        }

        if (theta is double newTheta)
        {
            if (newTheta < THETA_MIN || newTheta > THETA_MAX) throw new ArgumentOutOfRangeException(nameof(theta), "Theta must be between 0 and 1 (inclusive).");
            Theta = newTheta;
            ThetaSquared = newTheta * newTheta;
        }

        if (epsilon is double newEpsilon)
        {
            if (newEpsilon < EPSILON_MIN) throw new ArgumentOutOfRangeException(nameof(epsilon), "Epsilon must be greater than 0.0001.");
            Epsilon = newEpsilon;
            EpsilonSquared = newEpsilon * newEpsilon;
        }
    }

    #region Calculation Methods

    /// <inheritdoc/>
    public double DistanceSquaredSoftened(Vector2D pointA, Vector2D pointB)
        => pointA.DistanceToSquared(pointB) + EpsilonSquared;

    /// <inheritdoc/>
    public Vector2D Acceleration(Vector2D m1Position, Vector2D m2Position, double m2Mass, double? distanceSquaredSoftened = null)
    {
        double d_sq = distanceSquaredSoftened ?? DistanceSquaredSoftened(m1Position, m2Position);
        if (d_sq == 0) return Vector2D.Zero;

        // F = G * m1 * m2 / (d^2 + e^2)
        // a = F / m1
        // simplified to:
        // a = G * m2 / (d^2 + e^2)
        double accelerationMagnitude = G * m2Mass / d_sq;

        // Vector from target to source, then normalize to get direction
        // vector pointing from m1Position to m2Position
        Vector2D direction = (m2Position - m1Position).Normalized;

        return direction * accelerationMagnitude;
    }
    
    #endregion
}