using Physics;
using Physics.Bodies;
using Physics.Core;
using Physics.Models;
using Timer = Physics.Core.Timer;

namespace PhysicsTests;

[TestFixture]
[DefaultFloatingPointTolerance(1e-12)]
public class SimulationTests()
{
    Simulation _sim;

    #region Helpers

    Simulation GetNewSim() => _sim = new(
            timer: new Timer(
                simulationTime: 0,
                timeStep: 1
            ),
            quadTree: new(),
            calculator: new Calculator(
                g_SI: Calculator.G_SI_DEFAULT,  // Regular G
                theta: 0
            ),
            bodyManager: new BodyManager()
        );

    private ICelestialBody AddEnabled(BodyDataUpdates? partialData = null)
    {
        var body = _sim.Bodies.CreateBody(id => new CelestialBody(id, position: new(0, 0))); // set 0,0 position for update to override
        BodyDataUpdates data = (partialData ?? new()) with { Enabled = true };
        _sim.Bodies.TryUpdateBody(body.Id, data);
        return body;
    }

    private ICelestialBody AddDisabled(BodyDataUpdates? partialData = null)
    {
        var body = _sim.Bodies.CreateBody(id => new CelestialBody(id, position: new(0, 0))); // set 0,0 position for update to override
        BodyDataUpdates data = (partialData ?? new()) with { Enabled = false };
        _sim.Bodies.TryUpdateBody(body.Id, data);
        return body;
    }

    #region Test Helper Tests

    [Test]
    public void Helpers_AddEnabled_CreatesEnabledBody()
    {
        var bodyDefaultData = AddEnabled();
        var bodyEnabled = AddEnabled(new(Enabled: false));
        var bodyOtherData = AddEnabled(new(Mass: 2, PosY: 1));

        Assert.Multiple(() =>
        {
            Assert.That(_sim.Bodies.HasBody(bodyDefaultData.Id));
            Assert.That(_sim.Bodies.HasBody(bodyEnabled.Id));
            Assert.That(_sim.Bodies.HasBody(bodyOtherData.Id));
            Assert.That(_sim.Bodies.EnabledBodies, Is.EquivalentTo(new[] { bodyDefaultData, bodyEnabled, bodyOtherData }));
            Assert.That(bodyOtherData.Mass, Is.EqualTo(2));
            Assert.That(bodyOtherData.Position.X, Is.EqualTo(0));
            Assert.That(bodyOtherData.Position.Y, Is.EqualTo(1));
        });
    }

    [Test]
    public void Helpers_AddDisabled_CreatesDisabledBody()
    {
        var bodyDefaultData = AddDisabled();
        var bodyDisabled = AddDisabled(new(Enabled: true));
        var bodyOtherData = AddDisabled(new(Mass: 2, PosY: 1));

        Assert.Multiple(() =>
        {
            Assert.That(_sim.Bodies.HasBody(bodyDefaultData.Id));
            Assert.That(_sim.Bodies.HasBody(bodyDisabled.Id));
            Assert.That(_sim.Bodies.HasBody(bodyOtherData.Id));
            Assert.That(_sim.Bodies.EnabledBodies, Has.Count.EqualTo(0));
            Assert.That(bodyOtherData.Mass, Is.EqualTo(2));
            Assert.That(bodyOtherData.Position.X, Is.EqualTo(0));
            Assert.That(bodyOtherData.Position.Y, Is.EqualTo(1));
        });
    }

    #endregion

    #endregion

    [SetUp]
    public void TestSetup() => _sim = GetNewSim();

    #region Initialization

    [Test]
    public void Constructor_InitializedComponents_AreCorrect()
    {
        var sim = new Simulation(
            timer: new Timer(simulationTime: 4, timeStep: 2),
            quadTree: new(),
            calculator: new Calculator(
                g_SI: 21.4,
                theta: 0.99,
                epsilon: 15
            ),
            bodyManager: new BodyManager()
        );

        Assert.Multiple(() =>
        {
            Assert.That(sim.Timer.SimulationTime, Is.EqualTo(4));
            Assert.That(sim.Timer.TimeStep, Is.EqualTo(2));
            Assert.That(sim.Calculator.G_SI, Is.EqualTo(21.4));
            Assert.That(sim.Calculator.Theta, Is.EqualTo(0.99));
            Assert.That(sim.Calculator.Epsilon, Is.EqualTo(15));
        });
    }

    #endregion


    #region Step Function - Time

    [Test]
    public void StepFunction_AdvancesTime_ByCorrectAmount_WithOrWithoutBodies()
    {
        _sim.StepFunction();

        Assert.That(_sim.Timer.SimulationTime, Is.EqualTo(1));

        AddDisabled();
        AddDisabled();

        _sim.StepFunction();

        Assert.That(_sim.Timer.SimulationTime, Is.EqualTo(2));

        AddEnabled();
        AddEnabled();

        _sim.StepFunction();

        Assert.That(_sim.Timer.SimulationTime, Is.EqualTo(3));
    }

    [Test]
    public void StepFunction_RespectsChangesInTimerTimeStep()
    {
        var body = AddEnabled(new(Mass: 1, VelX: 1));
        _sim.StepFunction();

        Assert.Multiple(() =>
        {
            Assert.That(body.Position.X, Is.EqualTo(1));
            Assert.That(_sim.Timer.SimulationTime, Is.EqualTo(1));
        });

        _sim.Timer.Update(timeStep: 3);
        _sim.StepFunction();

        Assert.Multiple(() =>
        {
            Assert.That(body.Position.X, Is.EqualTo(4));
            Assert.That(_sim.Timer.SimulationTime, Is.EqualTo(4));
        });
    }

    [Test]
    public void StepFunction_WithZeroTimeStep_DoesNotChangeState()
    {
        _sim.Timer.Update(timeStep: 0);
        var body = AddEnabled(new(Mass: 1, PosX: 1, VelX: 5));
        var initialPosition = body.Position;
        var initialVelocity = body.Velocity;
        var initialTime = _sim.Timer.SimulationTime;

        _sim.StepFunction();

        Assert.Multiple(() =>
        {
            Assert.That(body.Position, Is.EqualTo(initialPosition));
            Assert.That(body.Velocity, Is.EqualTo(initialVelocity));
            Assert.That(_sim.Timer.SimulationTime, Is.EqualTo(initialTime));
        });
    }

    [Test]
    public void StepFunction_WithNegativeTimeStep_ReversesSimulation()
    {
        var body = AddEnabled(new(Mass: 1, PosY: 10, VelX: 1));
        var (Position, Velocity) = (body.Position, body.Velocity);

        _sim.Timer.Update(timeStep: 1);
        for(int i=0; i < 10; i++) _sim.StepFunction();

        _sim.Timer.Update(timeStep: -1);
        for(int i=0; i < 10; i++) _sim.StepFunction();

        Assert.Multiple(() =>
        {
            Assert.That(body.Position.X, Is.EqualTo(Position.X));
            Assert.That(body.Position.Y, Is.EqualTo(Position.Y));
            Assert.That(body.Velocity.X, Is.EqualTo(Velocity.X));
            Assert.That(body.Velocity.Y, Is.EqualTo(Velocity.Y));
        });
    }

    #endregion

    #region Step Function - Calculator

    [Test]
    public void StepFunction_RespectsChangesInCalculatorParameters()
    {
        var bodyA = AddEnabled(new(Mass: 1));
        AddEnabled(new(Mass: 1, PosX: 10));
        _sim.Calculator.Update(g_SI: 1e-11);    // Must use small value to avoid overflow during unit conversion

        _sim.StepFunction();
        var acc1 = bodyA.Acceleration.X;
        Assert.That(acc1, Is.GreaterThan(0));

        // Step forward to check what the acceleration would be if G remained the same.
        _sim.StepFunction();
        var acc2_sameG = bodyA.Acceleration.X;

        // Then step backward
        _sim.Timer.Update(timeStep: -1);
        _sim.StepFunction();
        Assert.That(bodyA.Acceleration.X, Is.EqualTo(acc1));

        // Change G
        _sim.Timer.Update(timeStep: 1);
        _sim.Calculator.Update(g_SI: 1e-13);
        _sim.StepFunction();
        var acc2_lessG = bodyA.Acceleration.X;

        Assert.That(acc2_lessG, Is.LessThan(acc2_sameG));
    }

    [Test]
    public void StepFunction_WithNonZeroTheta_UsesApproximation()
    {
        // Since the QuadTree has its own tests that verify correctness and accuracy, 
        // this test simply verifies that different theta values have any measurable expected effect in the final simulation.
        TestSetup();
        _sim.Calculator.Update(theta: 0);
        var zeroTheta_center = AddEnabled(new(Mass: 10));
        var zeroTheta_close = AddEnabled(new(Mass: 1, PosX: 1, VelY: 1));
        var zeroTheta_far = AddEnabled(new(Mass: 1, PosX: 100, PosY: 100));

        _sim.StepFunction();

        TestSetup();
        _sim.Calculator.Update(theta: 1);
        var oneTheta_center = AddEnabled(new(Mass: 10));
        var oneTheta_close = AddEnabled(new(Mass: 1, PosX: 1, VelY: 1));
        var oneTheta_far = AddEnabled(new(Mass: 1, PosX: 100, PosY: 100));

        _sim.StepFunction();

        var centerDiff = Math.Abs(oneTheta_center.Acceleration.Magnitude - zeroTheta_center.Acceleration.Magnitude);
        var closeDiff = Math.Abs(oneTheta_close.Acceleration.Magnitude - zeroTheta_close.Acceleration.Magnitude);
        var farDiff = Math.Abs(oneTheta_far.Acceleration.Magnitude - zeroTheta_far.Acceleration.Magnitude);

        Assert.Multiple(() =>
        {
            // The difference in acceleration between theta=0 and theta=1 
            // should be greater for a faraway body than for closer ones.
            Assert.That(farDiff, Is.GreaterThan(centerDiff));
            Assert.That(farDiff, Is.GreaterThan(closeDiff));
        });
    }

    #endregion


    #region Step Function - Bodies

    [Test]
    public void StepFunction_UpdatesEnabledBodies_IgnoresDisabled()
    {
        var centerBody = AddEnabled(new(Mass: 10));
        var bodyEast = AddEnabled(new(Mass: 1, PosX: 1));
        var bodyNorth_Disabled = AddDisabled(new(Mass: 1, PosY: 1));

        _sim.StepFunction();

        Assert.Multiple(() =>
        {
            Assert.That(centerBody.Position.X, Is.GreaterThan(0));  // Should move east
            Assert.That(centerBody.Position.Y, Is.EqualTo(0));      // Should not move
            Assert.That(bodyEast.Position.X, Is.LessThan(1));       // Should move towards center
            Assert.That(bodyEast.Position.Y, Is.EqualTo(0));        // Should not move N/S
            Assert.That(bodyNorth_Disabled.Position, Is.EqualTo(new Vector2D(0, 1)));   // Should not be affected at all
        });
    }

    [Test]
    public void StepFunction_WithSingleBody_MaintainsConstantVelocity()
    {
        var body = AddEnabled(new(Mass: 1, VelX: 1));

        for (int i = 0; i < 5; i++) _sim.StepFunction();

        Assert.Multiple(() =>
        {
            Assert.That(body.Position.X, Is.EqualTo(5));
            Assert.That(body.Position.Y, Is.Zero);
            Assert.That(body.Velocity, Is.EqualTo(new Vector2D(1, 0)));
            Assert.That(body.Acceleration, Is.EqualTo(Vector2D.Zero));
        });
    }


    [Test]
    public void StepFunction_OnBodyChange_ImmediatelyAffectsSystemOnNextStep()
    {
        // 1. Setup
        var bodyA = AddEnabled(new(Mass: 1));
        var bodyB = AddEnabled(new(Mass: 10, PosX: 20));
        var aAccPrev = bodyA.Acceleration.X;

        _sim.StepFunction();
        Assert.That(bodyA.Acceleration.X, Is.GreaterThan(aAccPrev));   // A accelerates towards B
        aAccPrev = bodyA.Acceleration.X;

        // 2. Reacts to added body
        var bodyC = AddEnabled(new(Mass: 10, PosX: -1));   // massive body close to A, opposite of B
        _sim.StepFunction();
        Assert.That(bodyA.Acceleration.X, Is.LessThan(aAccPrev));  // accelerates towards C
        aAccPrev = bodyA.Acceleration.X;

        // 3. Reacts to updated body
        _sim.Bodies.TryUpdateBody(bodyC.Id, new(Mass: 1));
        _sim.StepFunction();
        Assert.That(bodyA.Acceleration.X, Is.GreaterThan(aAccPrev));  // accelerates towards B
        aAccPrev = bodyA.Acceleration.X;

        // 3. Reacts to deleted body
        _sim.Bodies.TryDeleteBody(bodyB.Id);
        _sim.StepFunction();
        Assert.That(bodyA.Acceleration.X, Is.LessThan(aAccPrev));   // accelerates towards C 
    }

    [Test]
    public void StepFunction_WithCoincidentBodies_DoesNotProduceInfiniteForce()
    {
        var bodyA = AddEnabled(new(Mass: 1, PosX: 1, PosY: 1));
        var bodyB = AddEnabled(new(Mass: 10, PosX: 1, PosY: 1));
        _sim.Calculator.Update(epsilon: 0.01);
        _sim.StepFunction();

        Assert.Multiple(() =>
        {
            Assert.That(bodyA.Acceleration.X, Is.Zero);
            Assert.That(bodyA.Acceleration.Y, Is.Zero);
            Assert.That(bodyB.Acceleration.X, Is.Zero);
            Assert.That(bodyB.Acceleration.Y, Is.Zero);
        });
    }

    [Test]
    public void StepFunction_WithOnlyZeroMassBodies_UpdatesCorrectly()
    {
        var body_stationary = AddEnabled(new(Mass: 0, PosX: 1, PosY: 1));
        var body_moving = AddEnabled(new(Mass: 0, PosX: 2, PosY: 2, VelX: 1, VelY: 1));
        _sim.StepFunction();

        Assert.Multiple(() =>
        {
            Assert.That(body_stationary.Position.X, Is.EqualTo(1));
            Assert.That(body_stationary.Position.Y, Is.EqualTo(1));
            Assert.That(body_moving.Position.X, Is.EqualTo(3));
            Assert.That(body_moving.Position.Y, Is.EqualTo(3));
        });
    }

    [Test]
    public void StepFunction_TwoBodySystem_OneNegativeMass_CausesRunawayMotion()
    {
        var body_positive = AddEnabled(new(Mass: 1, PosX: 1));
        var body_negative = AddEnabled(new(Mass: -1, PosX: 0));
        _sim.StepFunction();

        var velX_pos = body_positive.Velocity.X;
        var velX_neg = body_negative.Velocity.X;

        Assert.Multiple(() =>
        {
            Assert.That(velX_pos, Is.Positive); // positive mass body is pushed away
            Assert.That(velX_neg, Is.Positive); // negative mass body follows positive mass body

            Assert.That(body_positive.Velocity.Y, Is.EqualTo(0));
            Assert.That(body_negative.Velocity.Y, Is.EqualTo(0));
        });

        _sim.StepFunction();

        var dist = body_negative.Position.DistanceTo(body_positive.Position);

        Assert.Multiple(() =>
        {   // X velocity of both increases in a runaway motion
            Assert.That(body_positive.Velocity.X, Is.GreaterThan(velX_pos));
            Assert.That(body_negative.Velocity.X, Is.GreaterThan(velX_neg));
            // but distance between them remains identical
            Assert.That(dist, Is.EqualTo(1));
        });
    }

    [Test]
    public void StepFunction_TwoBodySystem_BothNegativeMass_IsRepulsive()
    {
        // Two bodies with identical negative mass on the X axis
        var bodyA = AddEnabled(new(Mass: -1, PosX: 1));
        var bodyB = AddEnabled(new(Mass: -1, PosX: 0));
        _sim.StepFunction();

        var velX_a = bodyA.Velocity.X;
        var velX_b = bodyB.Velocity.X;
        var accX_a = bodyA.Acceleration.X;
        var accX_b = bodyB.Acceleration.X;

        Assert.Multiple(() =>
        {   
            // bodies are pushed away from one another
            Assert.That(velX_a, Is.Positive);
            Assert.That(velX_b, Is.Negative);
            // at the same absolute velocity
            Assert.That(Math.Abs(velX_b), Is.EqualTo(velX_a));
            // and the same acceleration
            Assert.That(Math.Abs(accX_b), Is.EqualTo(accX_a));
            // while the y axis is unaffected
            Assert.That(bodyA.Velocity.Y, Is.EqualTo(0));
            Assert.That(bodyB.Velocity.Y, Is.EqualTo(0));
        });

        _sim.StepFunction();

        Assert.Multiple(() =>
        {   // absolute acceleration of both decreases
            Assert.That(bodyA.Acceleration.X, Is.LessThan(accX_a));
            Assert.That(Math.Abs(bodyB.Acceleration.X), Is.LessThan(Math.Abs(accX_b)));
            // but remains identical to one another
            Assert.That(Math.Abs(bodyB.Acceleration.X), Is.EqualTo(bodyA.Acceleration.X));
        });
    }

    [Test]
    public void StepFunction_BodiesWithExtremeCoordinates_HandledCorrectly()
    {
        var bodyA = AddEnabled(new(Mass: 1, PosX: double.MaxValue - 1, PosY: double.MaxValue - 1));
        var bodyB = AddEnabled(new(Mass: 1, PosX: double.MinValue + 1, PosY: double.MinValue + 1));

        _sim.StepFunction();

        Assert.Multiple(() =>
        {   // At this distance, the acceleration should be near zero.
            Assert.That(bodyA.Acceleration.X, Is.EqualTo(0));
            Assert.That(bodyA.Acceleration.Y, Is.EqualTo(0));
            Assert.That(bodyB.Acceleration.X, Is.EqualTo(0));
            Assert.That(bodyB.Acceleration.Y, Is.EqualTo(0));
        });
    }

    #endregion


    #region Detailed Physics Tests

    [Test]
    [TestCase(27)]
    [TestCase(243)]
    [TestCase(2187)]
    public void StepFunction_ForTwoBodyOrbit_ConservesMomentum(int iterations)
    {
        // p_vec = m * v_vec
        static Vector2D GetP_Vec(ICelestialBody a, ICelestialBody b) => a.Velocity * a.Mass + b.Velocity * b.Mass;

        var bodyA = AddEnabled(new(Mass: 1, PosY: 1, VelX: 1));
        var bodyB = AddEnabled(new(Mass: 1, PosY: -1, VelX: -1));

        var p_initial = GetP_Vec(bodyA, bodyB);
        Assert.That(p_initial.Magnitude, Is.EqualTo(0));

        for (int i = 0; i < iterations; i++) _sim.StepFunction();

        var p_final = GetP_Vec(bodyA, bodyB);

        Assert.Multiple(() =>
        {
            Assert.That(p_final.X, Is.EqualTo(p_initial.X));
            Assert.That(p_final.Y, Is.EqualTo(p_initial.Y));
        });
    }

    [Test]
    [TestCase(27)]
    [TestCase(243)]
    [TestCase(2187)]
    public void StepFunction_4BodySystem_IsSymmetric_RemainsSymmetric(int iterations)
    {
        // clockwise rotating symmetrical system
        AddEnabled(new(Mass: 1, PosX: 1, PosY: 1, VelY: -1));
        AddEnabled(new(Mass: 1, PosX: 1, PosY: -1, VelX: -1));
        AddEnabled(new(Mass: 1, PosX: -1, PosY: -1, VelY: 1));
        AddEnabled(new(Mass: 1, PosX: -1, PosY: 1, VelX: 1));

        var (bodiesSameDistToCenter_initial, bodiesSameVelMag_initial, centerOfMass_initial) = CalcVars();

        Assert.Multiple(() =>
        {
            Assert.That(centerOfMass_initial, Is.EqualTo(Vector2D.Zero));
            Assert.That(bodiesSameDistToCenter_initial, Is.True);
            Assert.That(bodiesSameVelMag_initial, Is.True);
        });

        for (int i = 0; i < iterations; i++) _sim.StepFunction();

        var (bodiesSameDistToCenter_after, bodiesSameVelMag_after, centerOfMass_after) = CalcVars();

        Assert.Multiple(() =>
        {
            Assert.That(centerOfMass_initial, Is.EqualTo(Vector2D.Zero));
            Assert.That(bodiesSameDistToCenter_after, Is.True);
            Assert.That(bodiesSameVelMag_after, Is.True);
        });

        // Helper function for calculations
        (bool bodiesSameDistToCenter, bool bodiesSameVelMag, Vector2D centerOfMass) CalcVars()
        {
            List<double> distsToCenter = [];
            List<double> velMags = [];

            var weightedPositionSum = Vector2D.Zero;
            double mass = 0;
            for (int i = 0; i < _sim.Bodies.EnabledCount; i++)
            {
                var body = _sim.Bodies.EnabledBodies[i];
                mass += body.Mass;
                weightedPositionSum += body.Position * body.Mass;

                distsToCenter.Add(body.Position.DistanceTo(Vector2D.Zero));
                velMags.Add(body.Velocity.Magnitude);
            }

            return (
                bodiesSameDistToCenter: distsToCenter.Distinct().Count() == 1,
                bodiesSameVelMag: velMags.Distinct().Count() == 1,
                centerOfMass: mass != 0 ? weightedPositionSum / mass : Vector2D.Zero
            );
        }
    }

    #region Energy Conservation

    double GetTotalEnergy()
    {
        double G = _sim.Calculator.G;
        double E = _sim.Calculator.Epsilon;
        var bodies = _sim.Bodies.EnabledBodies;

        double e_kin_total = 0;
        double e_pot_total = 0;

        for (int i = 0; i < bodies.Count; i++)
        {
            var body = bodies[i];
            // e_kin = 0.5 * m * v^2
            e_kin_total += 0.5 * body.Mass * body.Velocity.MagnitudeSquared;

            // iterate over pairs to avoid double counting and self interaction
            for (int j = i + 1; j < bodies.Count; j++)
            {
                var otherBody = bodies[j];
                // e_pot = -G * m1 * m2 / r
                e_pot_total += -G * body.Mass * otherBody.Mass / (body.Position.DistanceTo(otherBody.Position) + E);
            }
        }

        return e_kin_total + e_pot_total;
    }

    [Test]
    [TestCase(100)]
    [TestCase(1000)]
    [TestCase(10000)]
    public void StepFunction_ForTwoBodyOrbit_EnergyRemainsBounded(int iterations)
    {
        AddEnabled(new(Mass: 1));
        AddEnabled(new(Mass: 1e-5, PosY: 5, VelX: 5));

        var initialEnergy = GetTotalEnergy();
        double maxDeviation = 0;

        for (int i = 0; i < iterations; i++)
        {
            _sim.StepFunction();
            var currentEnergy = GetTotalEnergy();
            var deviation = Math.Abs(initialEnergy - currentEnergy);
            if (deviation > maxDeviation) maxDeviation = deviation;
        }

        double percentDeviation = maxDeviation / Math.Abs(initialEnergy);
        Assert.That(percentDeviation, Is.LessThan(0.01));
    }

    #endregion    

    #endregion


    #region White Box

    [Test]
    [TestCase(50)]
    public void StepFunction_ForTwoBodyOrbit_ConservedEnergyError_ReflectsShadowHamiltonian(int iterations)
    {
        double GetTotalEnergyError_2Body(double timeStep)
        {
            TestSetup();    // reset sim
            _sim.Timer.Update(timeStep: timeStep);
            AddEnabled(new(Mass: 1, PosY: 1, VelX: 1));
            AddEnabled(new(Mass: 1, PosY: -1, VelX: -1));

            var e_total_initial = GetTotalEnergy();
            for (int i = 0; i < iterations; i++) _sim.StepFunction();
            var e_total_final = GetTotalEnergy();
            return e_total_initial - e_total_final;
        }

        // Run the same simulation multiple times with halving time steps
        var err_dt_8 = GetTotalEnergyError_2Body(timeStep: 8);
        var err_dt_4 = GetTotalEnergyError_2Body(timeStep: 4);
        var err_dt_2 = GetTotalEnergyError_2Body(timeStep: 2);
        var err_dt_1 = GetTotalEnergyError_2Body(timeStep: 1);

        Assert.Multiple(() =>
        {
            // Error should come from a single step with truncation error of O(dt²)
            // so we expect the error to decrease by a factor of roughly 4 whenever dt is halved.
            Assert.That(err_dt_8, Is.EqualTo(err_dt_4 * 4).Within(1e-3));
            Assert.That(err_dt_4, Is.EqualTo(err_dt_2 * 4).Within(1e-3));
            Assert.That(err_dt_2, Is.EqualTo(err_dt_1 * 4).Within(1e-3));
        });
    }

    #endregion
}
