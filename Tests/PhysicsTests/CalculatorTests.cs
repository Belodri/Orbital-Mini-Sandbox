using System.Net.Http.Headers;
using Physics;
using Physics.Bodies;
using Physics.Core;
using Physics.Models;

namespace PhysicsTests;

using static PhysicsTests.TestHelpers;

[TestFixture]
[DefaultFloatingPointTolerance(1e-12)]
public class CalculatorTests
{
    private readonly double DEFAULT_G = Calculator.GravitationalConstant_DEFAULT;
    private readonly double DEFAULT_THETA = Calculator.THETA_DEFAULT;
    private readonly double DEFAULT_EPSILON = Calculator.EPSILON_DEFAULT;
    private readonly IntegrationAlgorithm DEFAULT_ALGORITHM = Calculator.IntegrationAlgorithm_DEFAULT;

    #region Constructor

    [Test]
    public void Constructor_WithoutCustomValues_InitializesWithDefaultValues()
    {
        // Arrange & Act
        var calculator = new Calculator();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(calculator.GravitationalConstant, Is.EqualTo(DEFAULT_G));
            Assert.That(calculator.Theta, Is.EqualTo(DEFAULT_THETA));
            Assert.That(calculator.Epsilon, Is.EqualTo(DEFAULT_EPSILON));
            Assert.That(calculator.IntegrationAlgorithm, Is.EqualTo(DEFAULT_ALGORITHM));
        });
    }

    [Test]
    public void Constructor_WithCustomValues_InitializesPropertiesCorrectly()
    {
        // Arrange
        const double customG = 1.0;
        const double customTheta = 0.8;
        const double customEpsilon = 10.0;
        const IntegrationAlgorithm customAlgorithm = IntegrationAlgorithm.RungeKutta4;

        // Act
        var calculator = new Calculator(
            gravitationalConstant: customG,
            theta: customTheta,
            epsilon: customEpsilon,
            algorithm: customAlgorithm
        );

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(calculator.GravitationalConstant, Is.EqualTo(customG));
            Assert.That(calculator.Theta, Is.EqualTo(customTheta));
            Assert.That(calculator.Epsilon, Is.EqualTo(customEpsilon));
            Assert.That(calculator.IntegrationAlgorithm, Is.EqualTo(customAlgorithm));
        });
    }

    #endregion


    #region Update

    [Test]
    public void Update_CorrectlyChangesSingleAndMultipleProperties()
    {
        // Arrange
        var calculator = new Calculator(); // Start with default values
        const double newG = 2.5;
        const IntegrationAlgorithm newAlgorithm = IntegrationAlgorithm.SymplecticEuler;

        // Act
        // We are updating G and the algorithm, but leaving Theta and Epsilon unchanged.
        calculator.Update(
            gravitationalConstant: newG,
            integrationAlgorithm: newAlgorithm
        );

        // Assert
        Assert.Multiple(() =>
        {
            // Verify changed properties
            Assert.That(calculator.GravitationalConstant, Is.EqualTo(newG));
            Assert.That(calculator.IntegrationAlgorithm, Is.EqualTo(newAlgorithm));

            // Verify unchanged properties
            Assert.That(calculator.Theta, Is.EqualTo(DEFAULT_THETA));
            Assert.That(calculator.Epsilon, Is.EqualTo(DEFAULT_EPSILON));
        });
    }

    [Test]
    public void Update_WithAllNullParameters_DoesNotChangeProperties()
    {
        // Arrange
        var calculator = new Calculator(
            gravitationalConstant: 1, theta: 0.7, epsilon: 5, algorithm: IntegrationAlgorithm.RungeKutta4
        );

        var initialG = calculator.GravitationalConstant;
        var initialTheta = calculator.Theta;
        var initialEpsilon = calculator.Epsilon;
        var initialAlgorithm = calculator.IntegrationAlgorithm;

        // Act
        calculator.Update(); // Call with all default null parameters

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(calculator.GravitationalConstant, Is.EqualTo(initialG));
            Assert.That(calculator.Theta, Is.EqualTo(initialTheta));
            Assert.That(calculator.Epsilon, Is.EqualTo(initialEpsilon));
            Assert.That(calculator.IntegrationAlgorithm, Is.EqualTo(initialAlgorithm));
        });
    }

    [Test]
    public void Update_WithInvalidParameters_ClampsToValidRange()
    {
        // Arrange
        var calculator = new Calculator();
        // Assumes these consts exist on Calculator, matching your description.
        // We'll use reflection if they are private, or hard-code if they are stable.
        const double THETA_MIN = Calculator.THETA_MIN;
        const double THETA_MAX = Calculator.THETA_MAX;
        const double EPSILON_MIN = Calculator.EPSILON_MIN;

        // Act & Assert
        Assert.Multiple(() =>
        {
            // Test Theta clamping
            calculator.Update(theta: THETA_MAX + 1); // Above max
            Assert.That(calculator.Theta, Is.EqualTo(THETA_MAX));

            calculator.Update(theta: THETA_MIN - 1); // Below min
            Assert.That(calculator.Theta, Is.EqualTo(THETA_MIN));

            // Test Epsilon clamping
            calculator.Update(epsilon: EPSILON_MIN - 1); // Below min
            Assert.That(calculator.Epsilon, Is.EqualTo(EPSILON_MIN));
        });
    }

    #endregion


    #region EvaluateBody - Basic Evaluations

    [Test]
    public void EvaluateBody_InUniverseWithOnlySelf_ReturnsNull()
    {
        // Arrange
        var calculator = new Calculator();
        CelestialBody body = CreateBody(0);
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
        var gInSiUnits = Calculator.G_SI_PER_AC_FACTOR;
        var calculator = new Calculator(gravitationalConstant: gInSiUnits, epsilon: 0.0);

        // Get the actual epsilon value the calculator is using after clamping.
        var actualEpsilon = calculator.Epsilon;
        Assert.That(actualEpsilon, Is.EqualTo(Calculator.EPSILON_MIN)); // Verify our assumption

        CelestialBody body1 = CreateBody(1);
        body1.Update(mass: 6.0, position: new Vector2D(0, 0)); // mass in M☉, position in au

        CelestialBody body2 = CreateBody(2); // The body we will evaluate
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
        var gInSiUnits = Calculator.G_SI_PER_AC_FACTOR; // G_AC = 1.0

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
        var gInSiUnits = Calculator.G_SI_PER_AC_FACTOR; // G_AC = 1.0

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
        var gInSiUnits = Calculator.G_SI_PER_AC_FACTOR; // G_AC = 1.0
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
}


/*
6. EvaluateBody_InUniverseWithOnlySelf_ReturnsNull (Tests both "empty universe" and "no self-force" concepts)
7. EvaluateBody_TwoBodyProblem_CalculatesCorrectGravitationalAcceleration
8. EvaluateBody_EpsilonSoftening_ReducesForceAtCloseRange
9. EvaluateBody_BarnesHutTheta_AffectsForceApproximation
10. EvaluateBody_AtCenterOfSymmetricSystem_ReturnsNull
12. EvaluateBody_WithNegativeDeltaTime_IntegratesBackwardsInTime
13. EvaluateBody_IntegrationAlgorithm_SymplecticEuler_IsCorrect
14. EvaluateBody_IntegrationAlgorithm_RungeKutta4_IsCorrect
15. EvaluateBody_IntegrationAlgorithm_VelocityVerlet_IsCorrect

Constructor_WithoutCustomValues_InitializesWithDefaultValues
    Verifies that creating a Calculator with no arguments sets the public properties (GravitationalConstant, Theta, Epsilon, IntegrationAlgorithm) to their default values.

Constructor_WithCustomValues_InitializesPropertiesCorrectly
    Verifies that the constructor correctly accepts and assigns custom values provided by the user.

Update_Method_UpdatesSingleAndMultipleProperties
    Verifies that the update method correctly modifies the state.

Update_WithAllNullParameters_DoesNotChangeAnyProperties
    Verifies that calling Update with all null arguments results in no changes to the calculator's state. This is a crucial test for the intended behavior of the optional parameters.

EvaluateBody_InEmptyUniverse_ReturnsNull
    Verifies that a body with other gravitational forces acting on it experiences zero net acceleration. 
    According to the XML documentation, this should result in a null return.

EvaluateBody_TwoBodyProblem_CalculatesCorrectGravitationalAcceleration
    Verifies that the core implementation of Newton's Law of Universal Gravitation is correct by checking against a manually calculated result.

EvaluateBody_EpsilonSoftening_ReducesForceAtCloseRange
    Verifies that the softening factor Epsilon correctly reduces the gravitational force when two bodies are extremely close (potential division by 0 is handled within the QuadTree itself).

EvaluateBody_BarnesHutTheta_AffectsForceApproximation
    Verifies that the Theta parameter correctly controls the trade-off between accuracy and approximation in the Barnes-Hut algorithm.

EvaluateBody_AtCenterOfSymmetricSystem_ReturnsNull
    Tests a zero net-force scenario. By placing a body at the exact center of a symmetrical arrangement of other bodies (e.g., at the origin with four identical masses at (+-X, 0) and (0, +-Y)), the net gravitational force should be zero. This should also result in a null return.

EvaluateBody_WithZeroDeltaTime_ReturnsCurrentStateWithCalculatedAcceleration
    Verifies that a zero time step results in no change to position or velocity, but still returns the correctly calculated instantaneous acceleration. The method should not return null unless the net force is truly zero.
    The method should return an EvaluationResult where:
        - Position is the same as the body's starting position.
        - Velocity is the same as the body's starting velocity.
        - Acceleration is the non-zero, correctly calculated gravitational acceleration at that instant.

EvaluateBody_WithNegativeDeltaTime_IntegratesBackwardsInTime
    Verifies that providing a negative deltaTime correctly simulates the system backward. The resulting velocity should be adjusted in the opposite direction compared to a positive deltaTime, and the position should integrate "into the past."

EvaluateBody_IntegrationAlgorithm_SymplecticEuler_IsCorrect
    Verifies that a given integration algorithm is used correctly. Done via manual calculation of a simple two-body system.

EvaluateBody_IntegrationAlgorithm_RuttaKunge4_IsCorrect
    Verifies that a given integration algorithm is used correctly. Done via manual calculation of a simple two-body system.

EvaluateBody_IntegrationAlgorithm_VelocityVerlet_IsCorrect
    Verifies that a given integration algorithm is used correctly. Done via manual calculation of a simple two-body system.

*/