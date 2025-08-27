using Physics.Bodies;
using Physics.Core;
using Physics.Models;
using Timer = Physics.Core.Timer;

namespace PhysicsTests;

[TestFixture]
[DefaultFloatingPointTolerance(1e-12)]
public class SimulationCalcTests()
{
    Simulation _sim;

    [SetUp]
    public void TestSetup()
    {
        _sim = new(
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
    }

    [Test]
    public void StepFunction_AdvancesTime_WithOrWithoutBodies()
    {
        Assert.That(_sim.Timer.SimulationTime, Is.Zero);

        _sim.StepFunction();

        Assert.That(_sim.Timer.SimulationTime, Is.EqualTo(1));

        // Add disabled bodies
        _sim.Bodies.CreateBody(id => new CelestialBody(id));
        _sim.Bodies.CreateBody(id => new CelestialBody(id));

        _sim.StepFunction();

        Assert.That(_sim.Timer.SimulationTime, Is.EqualTo(2));

        // Add enabled bodies
        _sim.Bodies.CreateBody(id => new CelestialBody(id, enabled: true));
        _sim.Bodies.CreateBody(id => new CelestialBody(id, enabled: true));

        _sim.StepFunction();

        Assert.That(_sim.Timer.SimulationTime, Is.EqualTo(3));
    }

    [Test]
    public void StepFunction_UpdatesEnabledBodies_IgnoresDisabled()
    {
        var centerBody = _sim.Bodies.CreateBody(id => new CelestialBody(id, enabled: true, mass: 10, position: new(0, 0)));
        var bodyEast = _sim.Bodies.CreateBody(id => new CelestialBody(id, enabled: true, mass: 1, position: new(1, 0)));
        var bodyNorth_Disabled = _sim.Bodies.CreateBody(id => new CelestialBody(id, enabled: false, mass: 1, position: new(0, 1)));

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
}