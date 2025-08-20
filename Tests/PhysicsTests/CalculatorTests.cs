using Physics.Core;
using Physics.Models;

namespace PhysicsTests;

[TestFixture]
[DefaultFloatingPointTolerance(1e-12)]
public class CalculatorTests
{
    Calculator _calc = new();

    [SetUp]
    public void BeforeEachTest() => _calc = new Calculator();

    #region Constructor

    [Test]
    public void Constructor_WithoutCustomValues_InitializesWithDefaultValues()
    {
        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(_calc.G_SI, Is.EqualTo(Calculator.G_SI_DEFAULT));
            Assert.That(_calc.Theta, Is.EqualTo(Calculator.THETA_DEFAULT));
            Assert.That(_calc.Epsilon, Is.EqualTo(Calculator.EPSILON_DEFAULT));
        });
    }

    [Test]
    public void Constructor_WithCustomValues_InitializesPropertiesCorrectly()
    {
        // Arrange
        const double customG = 1.0;
        const double customTheta = 0.8;
        const double customEpsilon = 10.0;

        // Act
        var calculator = new Calculator(
            g_SI: customG,
            theta: customTheta,
            epsilon: customEpsilon
        );

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(calculator.G_SI, Is.EqualTo(customG));
            Assert.That(calculator.Theta, Is.EqualTo(customTheta));
            Assert.That(calculator.Epsilon, Is.EqualTo(customEpsilon));
        });
    }

    [Test]
    public void Constructor_WithInvalidParameters_ThrowsException()
    {
        // Act & Assert
        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Calculator(theta: Calculator.THETA_MIN - 0.5));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Calculator(theta: Calculator.THETA_MAX + 0.5));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Calculator(epsilon: Calculator.EPSILON_MIN - 0.5));
        });
    }

    #endregion

    #region Pre-Calculated Properties

    [Test]
    public void PreCalculatedProperties_InitializeOrUpdate_Correctly()
    {
        // Assert initialization
        Assert.Multiple(() =>
        {
            Assert.That(_calc.G_AC, Is.EqualTo(_calc.G_SI / Calculator.G_SI_PER_AC_FACTOR));    // correct unit conversion
            Assert.That(_calc.ThetaSquared, Is.EqualTo(_calc.Theta * _calc.Theta));
            Assert.That(_calc.EpsilonSquared, Is.EqualTo(_calc.Epsilon * _calc.Epsilon));
        });

        // Arrange
        var newG_SI = 2.0;
        var newTheta = 0.7;
        var newEpsilon = 1.0;

        // Act
        _calc.Update(
            g_SI: newG_SI,
            theta: newTheta,
            epsilon: newEpsilon
        );

        // Assert updates
        Assert.Multiple(() =>
        {
            Assert.That(_calc.G_AC, Is.EqualTo(newG_SI / Calculator.G_SI_PER_AC_FACTOR));    // correct unit conversion
            Assert.That(_calc.ThetaSquared, Is.EqualTo(newTheta * newTheta));
            Assert.That(_calc.EpsilonSquared, Is.EqualTo(newEpsilon * newEpsilon));
        });
    }

    #endregion


    #region Update

    [Test]
    public void Update_CorrectlyChangesSingleAndMultipleProperties()
    {
        // Arrange
        const double newG = 2.5;
        const double newTheta = 0.9;

        double prevEpsilon = _calc.Epsilon;

        // Act
        _calc.Update(
            g_SI: newG,
            theta: newTheta,
            epsilon: null
        );

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(_calc.G_SI, Is.EqualTo(newG));
            Assert.That(_calc.Theta, Is.EqualTo(newTheta));
            Assert.That(_calc.Epsilon, Is.EqualTo(prevEpsilon));
        });
    }

    [Test]
    public void Update_WithInvalidParameters_ThrowsException()
    {
        // Act & Assert
        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _calc.Update(theta: Calculator.THETA_MIN - 0.5));
            Assert.Throws<ArgumentOutOfRangeException>(() => _calc.Update(theta: Calculator.THETA_MAX + 0.5));
            Assert.Throws<ArgumentOutOfRangeException>(() => _calc.Update(epsilon: Calculator.EPSILON_MIN - 0.5));
        });
    }

    #endregion

    #region DistanceSquaredSoftened

    [Test]
    public void DistanceSquaredSoftened_AddsSofteningFactor()
    {
        // Arrange
        Vector2D p1 = new(1, 1);
        Vector2D p2 = new(5, 5);

        // Act
        double dist_sq = p1.DistanceToSquared(p2);
        double dist_sq_soft = _calc.DistanceSquaredSoftened(p1, p2);
        double diff = Math.Abs(dist_sq_soft - dist_sq);

        // Assert
        Assert.That(diff, Is.EqualTo(_calc.EpsilonSquared));
    }

    #endregion

    #region Acceleration

    [Test]
    public void Acceleration_Calculation_ShouldBeCorrect()
    {
        // Arrange
        Vector2D m1_pos = new(1, 2);
        Vector2D m2_pos = new(4, 6);
        double m2_mass = 100;

        // Manual calculations
        double dist_sq = m1_pos.DistanceToSquared(m2_pos);
        double dist_sq_soft = dist_sq + _calc.EpsilonSquared;

        // F = G * m1 * m2 / (d^2 + e^2)
        // a = F / m1
        // simplified: a = G * m2 / (d^2 + e^2)
        double a = _calc.G_AC * m2_mass / dist_sq_soft;

        // Vector from target to source, then normalize to get direction
        Vector2D direction = (m2_pos - m1_pos).Normalized;

        Vector2D accVector = a * direction;

        Assert.Multiple(() =>
        {
            // Without passing pre-squared-softened distance
            Assert.That(_calc.Acceleration(m1_pos, m2_pos, m2_mass), Is.EqualTo(accVector));
            // With passing pre-squared-softened distance
            Assert.That(_calc.Acceleration(m1_pos, m2_pos, m2_mass, dist_sq_soft), Is.EqualTo(accVector));
        });
    }
    
    [Test]
    public void Acceleration_Calculation_ShouldBeCorrect_Manual()
    {
        /*  Initial conditions

            p1 = {1,2}  // in units of astronomical units
            p2 = {4,6}  // in units of astronomical units
            m2 = 1      // in units of solar masses
            e = 0.001
            G_SI = 6.6743e-11   // in units of m³/kg/s²
            => G_AC ≈ 2.9591312e-4  // in units of au³/M☉/d²
        */
        Vector2D p1 = new(1, 2);
        Vector2D p2 = new(4, 6);
        double m2 = 1;
        _calc.Update(g_SI: 6.6743e-11, epsilon: 0.001);

        /*  Calculate distance
            dx = p1.x - p2.x 
                = 1 - 4 
                = -3
            dy = p1.y - p2.y 
                = 2 - 6 
                = -4
            d = sqrt(dx^2 + dy^2) 
                = sqrt(-3 * -3 + -4 * -4) 
                = sqrt(9 + 16) 
                = sqrt(25) 
                = 5
        */
        double d = p1.DistanceTo(p2);
        Assert.That(d, Is.EqualTo(5));

        /*  Calculate force magnitude
            a = G_AC * m2 / (d^2 + e^2)
                = 2.9591312e-4 * 1 / ( 5^2 + 0.001^2 )
                = 2.9591312e-4 / ( 25 + 0.000001 ) 
                = 2.9591312e-4 / 25.000001
                = 1.18365243e-5
        */
        double a = _calc.G_AC * m2 / (d * d + _calc.Epsilon * _calc.Epsilon);
        Assert.That(a, Is.EqualTo(1.18365243e-5));

        /*  Get direction vector
            vec_diff = (p2 - p1)
                = { (4-1) - (6-2) }
                = { 3, 4 }

            vec_dir = { vec_diff.x / d, vec_diff.y / d}
                = {3/5, 4/5}
                = {0.6, 0.8}
        */

        Vector2D vec_dir = new(0.6, 0.8);

        /*  Calculate acceleration vector
            vec_a = vec_dir * a
                = {0.6, 0.8} * 1.18365243e-5
                = { 7.10191458e-6, 9.46921944e-6 }
        */

        Vector2D vec_a = vec_dir * a;
        Vector2D compareVec = new (7.10191458e-6, 9.46921944e-6);

        Assert.Multiple(() =>
        {
            Assert.That(vec_a.X, Is.EqualTo(compareVec.X));
            Assert.That(vec_a.Y, Is.EqualTo(compareVec.Y));

            // Make sure that manually calculated acceleration vector is equal to the calculator method.
            Vector2D calcAcc = _calc.Acceleration(p1, p2, m2);
            Assert.That(vec_a.X, Is.EqualTo(calcAcc.X));
            Assert.That(vec_a.Y, Is.EqualTo(calcAcc.Y));
        });
    }

    #endregion
}

/*  OLD TESTS


    #region EvaluateBody - Basic Evaluations

    [Test]
    public void EvaluateBody_InUniverseWithOnlySelf_ReturnsNull()
    {
        // Arrange
        var calculator = new Calculator();
        ICelestialBody body = CreateBody(0);
        body.Update(mass: 1e6, position: new Vector2D(100, 100));

        // The "universe" is represented by the quadtree.
        // We build a tree containing only the body being evaluated.
        IGrid grid = new Grid();
        grid.Rebuild([body]);
        Assert.That(grid.Root, Is.Not.Null);    // Shouldn't be null since we just rebuilt but this confirms it and makes the compiler happy.

        // Act
        // Since a body exerts no gravitational force on itself, the net acceleration should be zero.
        // The method's contract specifies returning null in this case.
        var result = calculator.EvaluateBody(body, deltaTime: 1.0, gridRoot: grid.Root);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public void EvaluateBody_TwoBodyProblem_CalculatesCorrectGravitationalAcceleration()
    {
        // Arrange
        // Use G_SI that converts to G_AC = 1.0.
        // Initialize with epsilon=0.0, knowing it will be clamped to EPSILON_MIN by the constructor.
        var gInSiUnits = Calculator_New.G_SI_PER_AC_FACTOR;
        var calculator = new Calculator(gravitationalConstant: gInSiUnits, epsilon: 0.0);

        // Get the actual epsilon value the calculator is using after clamping.
        var actualEpsilon = calculator.Epsilon;
        Assert.That(actualEpsilon, Is.EqualTo(Calculator_New.EPSILON_MIN)); // Verify our assumption

        ICelestialBody body1 = CreateBody(1);
        body1.Update(mass: 6.0, position: new Vector2D(0, 0)); // mass in M☉, position in au

        ICelestialBody body2 = CreateBody(2); // The body we will evaluate
        body2.Update(mass: 1.0, position: new Vector2D(3, 0)); // mass in M☉, position in au


        var bodies = new[] { body1, body2 };
        IGrid grid = new Grid();
        grid.Rebuild(bodies);
        Assert.That(grid.Root, Is.Not.Null);

        // --- Manual Calculation (in AC units, including softening) ---
        // G = 1.0 au³/M☉/d²
        // m1 = 6.0 M☉, m2 = 1.0 M☉
        // r = 3.0 au, r² = 9.0
        // ε = actualEpsilon (from calculator), ε² = actualEpsilon * actualEpsilon
        // Force_Mag = G * (m1 * m2) / (r² + ε²)
        // Accel_Mag = Force_Mag / m2 = G * m1 / (r² + ε²)
        double r_squared = 3.0 * 3.0;
        double epsilon_squared = actualEpsilon * actualEpsilon;
        double accel_mag = 1.0 * 6.0 / (r_squared + epsilon_squared);

        // The force is attractive, so body2 is pulled towards body1 (in the -X direction).
        var expectedAcceleration = new Vector2D(-accel_mag, 0);

        // Act
        // Use deltaTime = 0 to get the instantaneous acceleration without changing position or velocity.
        var result = calculator.EvaluateBody(body2, deltaTime: 0, gridRoot: grid.Root);

        // Assert
        Assert.That(result, Is.Not.Null, "Result should not be null for a non-zero force scenario.");
        Assert.Multiple(() =>
        {
            Assert.That(result.Value.Acceleration.X, Is.EqualTo(expectedAcceleration.X));
            Assert.That(result.Value.Acceleration.Y, Is.EqualTo(expectedAcceleration.Y));

            // With deltaTime = 0, position and velocity should not change.
            Assert.That(result.Value.Position, Is.EqualTo(body2.Position));
            Assert.That(result.Value.Velocity, Is.EqualTo(body2.Velocity));
        });
    }

    [Test]
    public void EvaluateBody_EpsilonSoftening_ReducesForceAtCloseRange()
    {
        // Arrange
        var gInSiUnits = Calculator_New.G_SI_PER_AC_FACTOR; // G_AC = 1.0

        // Calculator 1: Uses a significant epsilon for softening.
        var calculatorWithSoftening = new Calculator(gravitationalConstant: gInSiUnits, epsilon: 1.0);
        var epsilon = calculatorWithSoftening.Epsilon; // Epsilon is 1.0

        // Calculator 2: Uses the minimum possible epsilon.
        var calculatorMinimalSoftening = new Calculator(gravitationalConstant: gInSiUnits, epsilon: 0.0);
        var minEpsilon = calculatorMinimalSoftening.Epsilon; // Epsilon is EPSILON_MIN

        Assert.That(epsilon, Is.GreaterThan(minEpsilon));

        var body1 = CreateBody(1);
        body1.Update(mass: 1000, position: new Vector2D(0, 0));

        // Place body2 very close to body1, where softening effects are most pronounced.
        var body2 = CreateBody(2);
        body2.Update(position: new Vector2D(0.1, 0));

        var bodies = new[] { body1, body2 };
        IGrid grid = new Grid();
        grid.Rebuild(bodies);
        Assert.That(grid.Root, Is.Not.Null);

        // Act
        var resultWithSoftening = calculatorWithSoftening.EvaluateBody(body2, 0, grid.Root);
        var resultMinimalSoftening = calculatorMinimalSoftening.EvaluateBody(body2, 0, grid.Root);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(resultWithSoftening, Is.Not.Null);
            Assert.That(resultMinimalSoftening, Is.Not.Null);
        });

        var accelMagnitudeWithSoftening = resultWithSoftening.Value.Acceleration.Magnitude;
        var accelMagnitudeMinimalSoftening = resultMinimalSoftening.Value.Acceleration.Magnitude;

        // The acceleration with a larger epsilon (stronger softening) must be less than
        // the acceleration with a smaller epsilon.
        // Formula: a = G*m1/(r²+ε²). Larger ε means larger denominator, hence smaller 'a'.
        Assert.That(accelMagnitudeWithSoftening, Is.LessThan(accelMagnitudeMinimalSoftening));
    }

    [Test]
    public void EvaluateBody_BarnesHutTheta_AffectsForceApproximation()
    {
        // Arrange
        var gInSiUnits = Calculator_New.G_SI_PER_AC_FACTOR; // G_AC = 1.0

        // Calculator 1: The "accurate" one. Theta = 0 forces it to visit every node,
        // equivalent to a direct N-Body summation.
        var accurateCalculator = new Calculator(gravitationalConstant: gInSiUnits, theta: 0.0);
        Assert.That(accurateCalculator.Theta, Is.EqualTo(0.0));

        // Calculator 2: The "approximate" one. A large theta encourages the algorithm
        // to treat distant groups of bodies as a single point mass.
        var approximateCalculator = new Calculator(gravitationalConstant: gInSiUnits, theta: 1.0);
        Assert.That(approximateCalculator.Theta, Is.EqualTo(1.0));

        // We create a distant "observer" body and a small, massive "cluster" of bodies.
        var observerBody = CreateBody(0);
        observerBody.Update(position: new Vector2D(100, 0)); // Far away on the X-axis

        var clusterBody1 = CreateBody(1);
        clusterBody1.Update(mass: 1e6, position: new Vector2D(0, 1));

        var clusterBody2 = CreateBody(2);
        clusterBody2.Update(mass: 1e6, position: new Vector2D(1, -1));

        var clusterBody3 = CreateBody(3);
        clusterBody3.Update(mass: 1e6, position: new Vector2D(-1, -1));

        var bodies = new[] { observerBody, clusterBody1, clusterBody2, clusterBody3 };
        IGrid grid = new Grid();
        grid.Rebuild(bodies);
        Assert.That(grid.Root, Is.Not.Null);

        // Act
        // We evaluate the force on the observerBody using both calculators.
        var accurateResult = accurateCalculator.EvaluateBody(observerBody, 0, grid.Root);
        var approximateResult = approximateCalculator.EvaluateBody(observerBody, 0, grid.Root);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(accurateResult, Is.Not.Null);
            Assert.That(approximateResult, Is.Not.Null);
        });

        var accurateAccel = accurateResult.Value.Acceleration;
        var approximateAccel = approximateResult.Value.Acceleration;

        Assert.Multiple(() =>
        {
            // 1. The primary assertion: The two calculations must produce different results.
            // This proves that the Theta parameter has an effect on the outcome.
            Assert.That(approximateAccel, Is.Not.EqualTo(accurateAccel),
                "Approximated acceleration should not be identical to the direct-summation acceleration.");

            // 2. Sanity check: While not identical, the approximation should be reasonable.
            // We assert that the magnitude of the approximated acceleration is within 1% of the accurate one.
            // This confirms the approximation is working correctly and not producing a wildly incorrect value.
            Assert.That(approximateAccel.Magnitude, Is.EqualTo(accurateAccel.Magnitude).Within(1).Percent,
                "Approximated acceleration magnitude should be very close to the accurate one.");
        });
    }

    [Test]
    public void EvaluateBody_AtCenterOfSymmetricSystem_ReturnsNull()
    {
        // Arrange
        var calculator = new Calculator();

        // The body to be tested, placed at the origin.
        var centerBody = CreateBody(0);
        centerBody.Update(position: new Vector2D(0, 0));

        // Create four identical masses and place them symmetrically around the center body.
        const double mass = 1e6;
        const double distance = 100;

        var bodyNorth = CreateBody(1);
        bodyNorth.Update(mass: mass, position: new Vector2D(0, distance));

        var bodySouth = CreateBody(2);
        bodySouth.Update(mass: mass, position: new Vector2D(0, -distance));

        var bodyEast = CreateBody(3);
        bodyEast.Update(mass: mass, position: new Vector2D(distance, 0));

        var bodyWest = CreateBody(4);
        bodyWest.Update(mass: mass, position: new Vector2D(-distance, 0));

        var bodies = new[] { centerBody, bodyNorth, bodySouth, bodyEast, bodyWest };
        IGrid grid = new Grid();
        grid.Rebuild(bodies);
        Assert.That(grid.Root, Is.Not.Null);

        // Act
        // The gravitational forces from the four outer bodies should perfectly cancel each other out.
        // The net force on the center body should be zero.
        var result = calculator.EvaluateBody(centerBody, deltaTime: 1.0, gridRoot: grid.Root);

        // Assert
        // The contract states that if net acceleration is zero, the method should return null.
        Assert.That(result, Is.Null);
    }

    #endregion

    #region EvaluateBody - Integrations - Time Reversal

    private static (Vector2D posDiff, Vector2D velDiff) Helpers_EvaluateBackwardsForwardsDifference(
        IntegrationAlgorithm algorithm,
        double deltaTime
    )
    {
        var calculator = new Calculator(algorithm: algorithm);
        var central = CreateBody(1);
        central.Update(mass: 1e6, position: new Vector2D(0, 0));

        var orbiting = CreateBody(2);
        orbiting.Update(mass: 1, position: new Vector2D(100, 0), velocity: new Vector2D(0, 10));

        var initialPosition = orbiting.Position;
        var initialVelocity = orbiting.Velocity;

        IGrid grid = new Grid();
        grid.Rebuild([central, orbiting]);
        if (grid.Root == null) throw new NullReferenceException("t0 grid root null");

        // Step forward in time
        var forwardResult = calculator.EvaluateBody(orbiting, deltaTime, grid.Root) ?? throw new NullReferenceException("forwardResult null");

        // Update only the orbiting body, keeping the center stationary
        orbiting.Update(
            position: forwardResult.Position,
            velocity: forwardResult.Velocity,
            acceleration: forwardResult.Acceleration
        );

        // Step backward in time from the intermediate state
        grid.Rebuild([central, orbiting]);
        if (grid.Root == null) throw new NullReferenceException("t1 grid root null");

        var backwardResult = calculator.EvaluateBody(orbiting, -deltaTime, grid.Root) ?? throw new NullReferenceException("backwardResult null");

        // Evaluate and return the difference between initial and end state.
        var posDiff = initialPosition - backwardResult.Position;
        var velDiff = initialVelocity - backwardResult.Velocity;

        return (posDiff, velDiff);
    }

    [Test]
    public void EvaluateBody_WithNegativeDeltaTime_IntegratesBackwards_SymplecticEuler()
    {
        // Arrange
        // Symplectic Euler is time-reversible. 

        const double deltaTime = 0.1;
        var (posDiff, velDiff) = Helpers_EvaluateBackwardsForwardsDifference(IntegrationAlgorithm.SymplecticEuler, deltaTime);

        // Assert
        // The difference vectors should be zero.
        Assert.Multiple(() =>
        {
            Assert.That(posDiff.Magnitude, Is.EqualTo(0));
            Assert.That(velDiff.Magnitude, Is.EqualTo(0));
        });
    }

    [Test]
    public void EvaluateBody_WithNegativeDeltaTime_IntegratesBackwards_VelocityVerlet()
    {
        // Arrange
        // Velocity Verlet is time-reversible. 

        const double deltaTime = 0.1;
        var (posDiff, velDiff) = Helpers_EvaluateBackwardsForwardsDifference(IntegrationAlgorithm.VelocityVerlet, deltaTime);

        // Assert
        // The difference vectors should be zero.
        Assert.Multiple(() =>
        {
            Assert.That(posDiff.Magnitude, Is.EqualTo(0));
            Assert.That(velDiff.Magnitude, Is.EqualTo(0));
        });
    }

    [Test]
    public void EvaluateBody_WithNegativeDeltaTime_IntegratesBackwards_RungeKutta4()
    {
        // Runge-Kutta-4 is NOT time reversable and will accumulate a small error.
        const double deltaTime = 0.1;
        var (posDiff, velDiff) = Helpers_EvaluateBackwardsForwardsDifference(IntegrationAlgorithm.RungeKutta4, deltaTime);

        // Assert
        // The magnitude of the difference vectors should be close to zero.
        var tolerance = 1e-7;
        Assert.Multiple(() =>
        {
            Assert.That(posDiff.Magnitude, Is.LessThan(tolerance));
            Assert.That(velDiff.Magnitude, Is.LessThan(tolerance));
        });
    }

    #endregion


    #region EvaluateBody - Integrations - Correctness

    [Test]
    public void EvaluateBody_IntegrationAlgorithm_SymplecticEuler_IsCorrect()
    {
        // Arrange
        var gInSiUnits = Calculator_New.G_SI_PER_AC_FACTOR; // G_AC = 1.0
        var calculator = new Calculator(
            gravitationalConstant: gInSiUnits,
            algorithm: IntegrationAlgorithm.SymplecticEuler
        );
        var actualEpsilon = calculator.Epsilon;
        const double deltaTime = 0.1;

        var centralBody = CreateBody(1);
        centralBody.Update(mass: 1e3, position: new(0, 0));

        var orbitingBody = CreateBody(2);
        orbitingBody.Update(
            position: new(10, 0),
            velocity: new(0, 5)
        );
        var initialPos = orbitingBody.Position;
        var initialVel = orbitingBody.Velocity;

        IGrid grid = new Grid();
        grid.Rebuild([centralBody, orbitingBody]);
        Assert.That(grid.Root, Is.Not.Null);

        // --- Manual Calculation for Symplectic Euler ---
        // 1. Calculate initial acceleration (a₀)
        // a = G * m_central / (r² + ε²) in the direction of -r
        double r_squared = initialPos.MagnitudeSquared;
        double epsilon_squared = actualEpsilon * actualEpsilon;
        double accel_mag = 1.0 * 1e3 / (r_squared + epsilon_squared);
        var initialAccel = -initialPos.Normalized * accel_mag;

        // 2. Calculate new velocity: v₁ = v₀ + a₀ * dt
        var expectedVel = initialVel + initialAccel * deltaTime;

        // 3. Calculate new position: p₁ = p₀ + v₁ * dt
        var expectedPos = initialPos + expectedVel * deltaTime;

        // Act
        var result = calculator.EvaluateBody(orbitingBody, deltaTime, grid.Root);
        Assert.That(result, Is.Not.Null);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.Value.Position, Is.EqualTo(expectedPos));
            Assert.That(result.Value.Velocity, Is.EqualTo(expectedVel));
            Assert.That(result.Value.Acceleration, Is.EqualTo(initialAccel));
        });
    }


    #endregion

*/
