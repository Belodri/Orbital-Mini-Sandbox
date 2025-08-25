using Physics.Bodies;
using Physics.Core;
using Physics.Models;

namespace PhysicsTests;

#region Mocks

public record MockBody(int Id, double Mass, Vector2D Position) : ICelestialBody
{
    public int Id { get; } = Id;
    public double Mass { get; } = Mass;
    public Vector2D Position { get; } = Position;
    // Boilerplate
    bool ICelestialBody.Enabled => throw new NotImplementedException();
    Vector2D ICelestialBody.Velocity => throw new NotImplementedException();
    Vector2D ICelestialBody.VelocityHalfStep => throw new NotImplementedException();
    Vector2D ICelestialBody.Acceleration => throw new NotImplementedException();
    event Action<ICelestialBody>? ICelestialBody.EnabledChanged { add { } remove { } }
    void ICelestialBody.Update(bool? enabled, double? mass, double? posX, double? posY, double? velX, double? velY, double? velX_half, double? velY_half, double? accX, double? accY) => throw new NotImplementedException();
    void ICelestialBody.Update(bool? enabled, double? mass, Vector2D? position, Vector2D? velocity, Vector2D? velocityHalfStep, Vector2D? acceleration) => throw new NotImplementedException();
}

public class MockCalculator(double Theta = 0, double Epsilon = 0.0001) : ICalculator
{
    public double Theta { get; } = Theta;
    public double ThetaSquared { get; } = Theta * Theta;
    public double Epsilon { get; } = Epsilon;
    public double EpsilonSquared { get; } = Epsilon * Epsilon;
    public double DistanceSquaredSoftened(Vector2D pointA, Vector2D pointB) => Math.Pow(pointA.X - pointB.X, 2) + Math.Pow(pointA.Y - pointB.Y, 2) + EpsilonSquared;
    public Vector2D Acceleration(Vector2D m1Position, Vector2D m2Position, double m2Mass, double? distanceSquaredSoftened = null)
    {
        double d_sq_softened = distanceSquaredSoftened ?? DistanceSquaredSoftened(m1Position, m2Position);
        double distance = Math.Sqrt(d_sq_softened);
        double magnitude = m2Mass / d_sq_softened;
        return m2Position - m1Position / distance * magnitude;
    }
    // Boilerplate
    double ICalculator.G_SI => throw new NotImplementedException();  // Ignore gravity for these tests
    double ICalculator.G_AC => throw new NotImplementedException();
    void ICalculator.Update(double? g_SI, double? theta, double? epsilon) => throw new NotImplementedException();
}

#endregion

[TestFixture]
[DefaultFloatingPointTolerance(1e-12)]
public class QuadTreeTestsBlackBox
{
    private QuadTree _tree;
    private MockCalculator _calcThetaZero = new();
    private MockCalculator _calcThetaHalf = new(0.5);
    private static MockBody MakeBody(int id, double mass, double pos_x, double pos_y)
        => new(id, mass, new(pos_x, pos_y));

    private bool VectorApproxEqual(Vector2D vec_a, Vector2D vec_b) => vec_a.ApproximatelyEquals(vec_b, 1e-12);

    [SetUp]
    public void TestSetup() => _tree = new();

    #region Reset

    [Test]
    [TestCase(11, 0, 10, 10)] // minX > maxX
    [TestCase(0, 11, 10, 10)] // minY > maxY
    public void Reset_WithInvalidBoundaries_ThrowsArgumentException(double minX, double minY, double maxX, double maxY)
        => Assert.Throws<ArgumentException>(() => _tree.Reset(minX, minY, maxX, maxY));

    [Test]
    [TestCase(0)]
    [TestCase(-1)]
    public void Reset_WithNonPositiveExpectedBodies_ThrowsArgumentException(int expectedBodies)
        => Assert.Throws<ArgumentException>(() => _tree.Reset(0, 0, 10, 10, expectedBodies));

    [Test]
    public void Reset_AfterInsertions_ClearsPreviousState()
    {
        // 1. Setup with a body
        _tree.Reset(0, 0, 100, 100);
        var bodyA = MakeBody(1, 100, 10, 10);
        _tree.InsertBody(bodyA);
        _tree.Evaluate();

        // 2. Reset the tree
        _tree.Reset(0, 0, 100, 100);

        // 3. Add a new, different body
        var bodyB = MakeBody(2, 50, 90, 90);
        _tree.InsertBody(bodyB);
        _tree.Evaluate();

        // 4. Verify acceleration on a test body. It should ONLY be affected by bodyB.
        // If the tree wasn't cleared, bodyA would also contribute to the acceleration.
        var testBody = MakeBody(3, 1, 0, 0);
        var acceleration = _tree.CalcAcceleration(testBody, _calcThetaZero);

        // Calculate expected acceleration ONLY from bodyB
        var expectedAcceleration = _calcThetaZero.Acceleration(testBody.Position, bodyB.Position, bodyB.Mass);

        Assert.That(VectorApproxEqual(acceleration, expectedAcceleration), Is.True);
    }

    #endregion


    #region InsertBody

    [Test]
    public void InsertBody_AfterEvaluate_ThrowsInvalidOperationException()
    {
        _tree.Reset(0, 0, 10, 10);
        _tree.InsertBody(MakeBody(1, 1, 5, 5));
        _tree.Evaluate();

        // Attempt to insert after evaluation
        var lateBody = MakeBody(2, 1, 6, 6);
        Assert.Throws<InvalidOperationException>(() => _tree.InsertBody(lateBody));
    }

    [Test]
    public void InsertBody_OutsideBounds_ThrowsArgumentException()
    {
        _tree.Reset(0, 0, 100, 100);
        var bodyOutside = MakeBody(1, 10, 200, 200);
        Assert.Throws<ArgumentException>(() => _tree.InsertBody(bodyOutside));
    }

    [Test]
    public void InsertBody_ExactlyOnBounds_DoesNotThrow()
    {
        _tree.Reset(-10, -10, 10, 10);
        var bodyOnBounds_NE = MakeBody(1, 10, 10, 10);
        var bodyOnBounds_SE = MakeBody(2, 10, 10, -10);
        var bodyOnBounds_NW = MakeBody(3, 10, -10, 10);
        var bodyOnBounds_SW = MakeBody(4, 10, -10, -10);

        Assert.Multiple(() =>
        {
            Assert.DoesNotThrow(() => _tree.InsertBody(bodyOnBounds_NE));
            Assert.DoesNotThrow(() => _tree.InsertBody(bodyOnBounds_SE));
            Assert.DoesNotThrow(() => _tree.InsertBody(bodyOnBounds_NW));
            Assert.DoesNotThrow(() => _tree.InsertBody(bodyOnBounds_SW));
        });
    }

    #endregion


    #region Evaluate

    [Test]
    public void Evaluate_OnEmptyTree_DoesNotThrow()
    {
        _tree.Reset(0, 0, 10, 10);
        Assert.DoesNotThrow(() => _tree.Evaluate());
    }

    [Test]
    public void Evaluate_RepeatedCalls_DoNotThrow()
    {
        _tree.Reset(0, 0, 10, 10);
        _tree.InsertBody(MakeBody(1, 1, 5, 5));
        _tree.Evaluate();
        Assert.DoesNotThrow(() => _tree.Evaluate());
    }

    #endregion


    #region CalcAcceleration

    [Test]
    public void CalcAcceleration_BeforeEvaluate_ThrowsInvalidOperationException()
    {
        _tree.Reset(0, 0, 10, 10);
        var body = MakeBody(1, 1, 5, 5);
        _tree.InsertBody(body);
        Assert.Throws<InvalidOperationException>(() => _tree.CalcAcceleration(body, _calcThetaZero));
    }

    [Test]
    public void CalcAcceleration_EmptyTree_ReturnsZeroVector()
    {
        _tree.Reset(0, 0, 100, 100);
        _tree.Evaluate();
        var body = MakeBody(1, 10, 50, 50);
        var acceleration = _tree.CalcAcceleration(body, _calcThetaZero);

        Assert.That(acceleration, Is.EqualTo(Vector2D.Zero));
    }

    [Test]
    public void CalcAcceleration_WithZeroMassBody_ContributesNoForce()
    {
        _tree.Reset(0, 0, 100, 100);
        var body_mass = MakeBody(1, 100, 0, 0);
        var body_zeroMass = MakeBody(2, 0, 50, 0);
        var testBody = MakeBody(3, 1, 25, 0);

        _tree.InsertBody(body_mass);
        _tree.InsertBody(body_zeroMass);
        _tree.InsertBody(testBody);
        _tree.Evaluate();

        var accOnTestBody = _tree.CalcAcceleration(testBody, _calcThetaZero);

        // Expected result should only include acceleration from body with mass
        var expectedAcc = _calcThetaZero.Acceleration(testBody.Position, body_mass.Position, body_mass.Mass);

        Assert.That(VectorApproxEqual(accOnTestBody, expectedAcc), Is.True);
    }

    [Test]
    public void CalcAcceleration_SingleBodyInTree_ExperiencesZeroAccelerationFromItself()
    {
        _tree.Reset(0, 0, 100, 100);
        var body = MakeBody(1, 10, 50, 50);
        _tree.InsertBody(body);
        _tree.Evaluate();

        var acceleration = _tree.CalcAcceleration(body, _calcThetaZero);

        Assert.That(acceleration, Is.EqualTo(Vector2D.Zero));
    }

    [Test]
    public void CalcAcceleration_TwoBodyProblem_IsCorrect()
    {
        _tree.Reset(-100, -100, 100, 100);
        var body1 = MakeBody(1, 100, -10, 10);
        var body2 = MakeBody(2, 200, 10, 0);

        _tree.InsertBody(body1);
        _tree.InsertBody(body2);
        _tree.Evaluate();

        // Calculate acceleration ON body1 DUE TO body2
        var accOnBody1 = _tree.CalcAcceleration(body1, _calcThetaZero);

        // Calculate the expected direct result
        var expectedAcc = _calcThetaZero.Acceleration(body1.Position, body2.Position, body2.Mass);

        Assert.That(VectorApproxEqual(accOnBody1, expectedAcc), Is.True);
    }

    [Test]
    public void CalcAcceleration_SymmetricBodies_YieldsZeroNetForceAtCenter()
    {
        _tree.Reset(-100, -100, 100, 100);
        var testBody = MakeBody(0, 1, 0, 0);

        double mass = 1000;
        _tree.InsertBody(testBody);
        _tree.InsertBody(MakeBody(1, mass, 50, 50));
        _tree.InsertBody(MakeBody(2, mass, -50, 50));
        _tree.InsertBody(MakeBody(3, mass, 50, -50));
        _tree.InsertBody(MakeBody(4, mass, -50, -50));
        _tree.Evaluate();

        var acceleration = _tree.CalcAcceleration(testBody, _calcThetaZero);

        // Due to perfect symmetry, the net force at the origin should be zero.
        Assert.That(VectorApproxEqual(acceleration, Vector2D.Zero), Is.True);
    }

    [Test]
    public void CalcAcceleration_DistantCluster_BehavesLikePointMass()
    {
        _tree.Reset(-1000, -1000, 1000, 1000);

        // A test body far away from the cluster
        var testBody = MakeBody(0, 1, -800, -800); 
        _tree.InsertBody(testBody);

        // A tight cluster of bodies far from the origin
        double clusterMass = 0;
        Vector2D weightedPositionSum = Vector2D.Zero;
        for (int i = 1; i <= 5; i++)
        {
            double pos = 800 + 1 / i;
            var body = MakeBody(i, 100, pos, pos);
            _tree.InsertBody(body);
            clusterMass += body.Mass;
            weightedPositionSum += body.Position * body.Mass;
        }
        Vector2D clusterCenterOfMass = weightedPositionSum / clusterMass;

        _tree.Evaluate();
        
        // Get acceleration from the QuadTree with Theta = 0.5
        var quadTreeAcceleration = _tree.CalcAcceleration(testBody, _calcThetaHalf);

        // Get expected acceleration from a single point-mass representing the cluster
        var pointMassAcceleration = _calcThetaZero.Acceleration(testBody.Position, clusterCenterOfMass, clusterMass);

        // They should be very close, but not necessarily identical.
        Assert.Multiple(() =>
        {
            Assert.That(quadTreeAcceleration.X, Is.EqualTo(pointMassAcceleration.X).Within(1).Percent);
            Assert.That(quadTreeAcceleration.Y, Is.EqualTo(pointMassAcceleration.Y).Within(1).Percent);
        });
    }

    [Test]
    public void CalcAcceleration_InsideOverloadedCluster_IsCorrect()
    {
        _tree.Reset(-1000, -1000, 1000, 1000);

        // Test body near one corner to force clustering
        var testBody = MakeBody(0, 1, 800, 800);
        _tree.InsertBody(testBody);

        // Extremely dense cluster near the test body
        var acc_sumDirect = Vector2D.Zero;
        for (int i = 1; i <= 5; i++)
        {
            double pos = 800 + 1/i;
            var otherBody = MakeBody(i, 10, pos, pos);
            _tree.InsertBody(otherBody);

            // Get acceleration directly and sum it
            acc_sumDirect += _calcThetaHalf.Acceleration(testBody.Position, otherBody.Position, otherBody.Mass);
        }

        _tree.Evaluate();

        // Get acceleration from the QuadTree with Theta = 0.5
        var acc_tree = _tree.CalcAcceleration(testBody, _calcThetaHalf);

        // They should be identical
        Assert.Multiple(() =>
        {
            Assert.That(acc_tree.X, Is.EqualTo(acc_sumDirect.X));
            Assert.That(acc_tree.Y, Is.EqualTo(acc_sumDirect.Y));
        });
        //Assert.That(VectorApproxEqual(acc_tree, acc_sumDirect), Is.True);
    }

    #endregion
}
