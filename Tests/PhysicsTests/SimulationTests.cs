using Physics;
using Physics.Bodies;
using Physics.Core;
using Timer = Physics.Core.Timer;

namespace PhysicsTests;

[TestFixture]
[DefaultFloatingPointTolerance(1e-12)]
public class SimulationTests
{
    Simulation _sim;

    [SetUp]
    public void TestSetup()
    {
        _sim = new(
            timer: new Timer(),
            quadTree: new(),
            calculator: new Calculator()
        );
    }

    #region Constructor

    [Test]
    public void Constructor_WithEmptyBodies_InitializesCorrectly()
    {
        Simulation sim_emptyBodies = new(
            timer: new Timer(),
            quadTree: new(),
            calculator: new Calculator(),
            bodies: []
        );

        Assert.That(sim_emptyBodies.Bodies, Has.Count.EqualTo(0));
    }

    [Test]
    public void Constructor_WithInitialBodies_AddsAllBodiesSuccessfully()
    {
        CelestialBody[] initialBodies = [new CelestialBody(0), new CelestialBody(1)];
        Simulation sim_emptyBodies = new(
            timer: new Timer(),
            quadTree: new(),
            calculator: new Calculator(),
            bodies: initialBodies
        );

        Assert.Multiple(() =>
        {
            Assert.That(sim_emptyBodies.Bodies, Has.Count.EqualTo(2));
            Assert.That(sim_emptyBodies.Bodies.ContainsKey(0), Is.True);
            Assert.That(sim_emptyBodies.Bodies.ContainsKey(1), Is.True);
            Assert.That(sim_emptyBodies.Bodies.GetValueOrDefault(0), Is.EqualTo(initialBodies[0]));
            Assert.That(sim_emptyBodies.Bodies.GetValueOrDefault(1), Is.EqualTo(initialBodies[1]));
        });
    }

    [Test]
    public void Constructor_WithDuplicateBodyIds_ThrowsArgumentException()
    {
        CelestialBody[] bodies = [new(0), new(0), new(1)];
        Assert.Throws<ArgumentException>(() => new Simulation(
            timer: new Timer(),
            quadTree: new(),
            calculator: new Calculator(),
            bodies: bodies
        ));
    }

    #endregion

    #region Body Management (Adding)

    [Test]
    public void CreateBody_WhenCalled_GeneratesUniqueIdAndAddsBody()
    {
        var addedBody = _sim.CreateBody(id => new CelestialBody(id));

        Assert.Multiple(() =>
        {
            Assert.That(addedBody.Id, Is.EqualTo(0));
            Assert.That(_sim.Bodies, Has.Count.EqualTo(1));
            Assert.That(_sim.Bodies.GetValueOrDefault(0), Is.EqualTo(addedBody));
        });
    }

    [Test]
    public void CreateBody_WhenCalledMultipleTimes_GeneratesUniqueIds()
    {
        var body0 = _sim.CreateBody(id => new CelestialBody(id));
        var body1 = _sim.CreateBody(id => new CelestialBody(id));

        Assert.That(body0.Id, Is.Not.EqualTo(body1.Id));
    }

    [Test]
    public void CreateBody_AfterAddingBodyWithSkippedId_GeneratesNextAvailableId()
    {
        _sim.TryAddBody(new CelestialBody(2));
        Assert.That(_sim.Bodies.ContainsKey(2), Is.True);

        var body0 = _sim.CreateBody(id => new CelestialBody(id));
        var body1 = _sim.CreateBody(id => new CelestialBody(id));
        var body3 = _sim.CreateBody(id => new CelestialBody(id));

        Assert.Multiple(() =>
        {
            Assert.That(body0.Id, Is.EqualTo(0));
            Assert.That(body1.Id, Is.EqualTo(1));
            Assert.That(body3.Id, Is.EqualTo(3));
        });
    }

    [Test]
    public void CreateBody_OverMaxBodies_ThrowsInvalidOperationException()
    {
        for (int i = 0; i < Simulation.MAX_BODIES; i++)
        {
            _sim.CreateBody(id => new CelestialBody(id));
        }

        Assert.That(_sim.Bodies, Has.Count.EqualTo(Simulation.MAX_BODIES));
        Assert.Throws<InvalidOperationException>(() => _sim.CreateBody(id => new CelestialBody(id)));
    }

    [Test]
    public void TryAddBody_WithNewBody_ReturnsTrueAndAddsBody()
    {
        var body = new CelestialBody(7);
        var returnValue = _sim.TryAddBody(body);

        Assert.Multiple(() =>
        {
            Assert.That(returnValue, Is.True);
            Assert.That(_sim.Bodies.GetValueOrDefault(7), Is.EqualTo(body));
        });
    }

    [Test]
    public void TryAddBody_WithExistingId_ReturnsFalseAndDoesNotAddBody()
    {
        var body0 = _sim.CreateBody(id => new CelestialBody(id, mass: 1));
        Assert.Multiple(() =>
        {
            Assert.That(_sim.Bodies, Has.Count.EqualTo(1));
            Assert.That(_sim.Bodies.GetValueOrDefault(0), Is.EqualTo(body0));
        });

        var returnValue = _sim.TryAddBody(new CelestialBody(0, mass: 5));
        Assert.Multiple(() =>
        {
            Assert.That(returnValue, Is.False);
            Assert.That(_sim.Bodies.GetValueOrDefault(0), Is.EqualTo(body0));
        });
    }

    #endregion


    #region Body Management (Deleting)

    [Test]
    public void TryDeleteBody_ById_WithExistingId_ReturnsTrueAndRemovesBody()
    {
        var bodyId = _sim.CreateBody(id => new CelestialBody(id)).Id;
        Assert.That(_sim.Bodies.ContainsKey(bodyId), Is.True);

        var isDeleted = _sim.TryDeleteBody(bodyId);

        Assert.Multiple(() =>
        {
            Assert.That(isDeleted, Is.True);
            Assert.That(_sim.Bodies.ContainsKey(bodyId), Is.False);
        });
    }

    [Test]
    public void TryDeleteBody_ById_WithNonExistentId_ReturnsFalse()
    {
        var bodyId = _sim.CreateBody(id => new CelestialBody(id)).Id;
        Assert.That(_sim.Bodies.ContainsKey(bodyId), Is.True);

        int nonExistentId = bodyId + 1;

        var isDeleted = _sim.TryDeleteBody(nonExistentId);
        Assert.Multiple(() =>
        {
            Assert.That(isDeleted, Is.False);
            Assert.That(_sim.Bodies.ContainsKey(bodyId), Is.True);
        });
    }

    [Test]
    public void TryDeleteBody_ByInstance_WithCorrectInstance_ReturnsTrueAndRemovesBody()
    {
        var body = new CelestialBody(5);
        _sim.TryAddBody(body);
        Assert.That(_sim.Bodies.ContainsKey(body.Id), Is.True);

        var isDeleted = _sim.TryDeleteBody(body);

        Assert.Multiple(() =>
        {
            Assert.That(isDeleted, Is.True);
            Assert.That(_sim.Bodies.ContainsKey(body.Id), Is.False);
        });
    }

    [Test]
    public void TryDeleteBody_ByInstance_WithDifferentInstanceSameId_ReturnsFalse()
    {
        var simBody = _sim.CreateBody(id => new CelestialBody(id));
        var otherBody = new CelestialBody(simBody.Id);

        var isDeleted = _sim.TryDeleteBody(otherBody);

        Assert.Multiple(() =>
        {
            Assert.That(isDeleted, Is.False);
            Assert.That(_sim.Bodies.GetValueOrDefault(simBody.Id), Is.EqualTo(simBody));
        });
    }

    #endregion


    #region Event Notifications

    [Test]
    public void BodyAdded_Event_OnCreateBody_IsRaisedOnSuccessfulAdd()
    {
        List<int> bodyIdsAdded = [];
        _sim.BodyAdded += body => bodyIdsAdded.Add(body.Id);
        for (int i = 0; i < 3; i++) _sim.CreateBody(id => new CelestialBody(id));

        Assert.That(bodyIdsAdded, Has.Count.EqualTo(3));
        Assert.That(bodyIdsAdded, Is.EqualTo(new List<int>([0, 1, 2])));
    }

    [Test]
    public void BodyAdded_Event_OnTryAddBody_IsRaisedOnSuccessfulAdd()
    {
        List<int> bodyIdsAdded = [];
        _sim.BodyAdded += body => bodyIdsAdded.Add(body.Id);
        for (int i = 0; i < 3; i++) _sim.TryAddBody(new CelestialBody(i));

        Assert.That(bodyIdsAdded, Has.Count.EqualTo(3));
        Assert.That(bodyIdsAdded, Is.EqualTo(new List<int>([0, 1, 2])));
    }

    [Test]
    public void BodyAdded_Event_IsNotRaisedOnFailedAdd()
    {
        // Add blocking body
        _sim.TryAddBody(new CelestialBody(1));

        List<int> bodyIdsAdded = [];
        _sim.BodyAdded += body => bodyIdsAdded.Add(body.Id);

        for (int i = 0; i < 3; i++) _sim.TryAddBody(new CelestialBody(i));

        Assert.That(bodyIdsAdded, Has.Count.EqualTo(2));
        Assert.That(bodyIdsAdded, Is.EqualTo(new List<int>([0, 2])));
    }

    [Test]
    public void BodyRemoved_Event_IsRaisedOnSuccessfulDelete()
    {
        List<int> bodyIdsRemoved = [];
        _sim.BodyRemoved += bodyIdsRemoved.Add;

        for (int i = 0; i < 3; i++) _sim.TryAddBody(new CelestialBody(i));

        _sim.TryDeleteBody(1);
        _sim.TryDeleteBody(2);

        Assert.That(bodyIdsRemoved, Has.Count.EqualTo(2));
        Assert.That(bodyIdsRemoved, Is.EqualTo(new List<int>([1, 2])));
    }

    [Test]
    public void BodyRemoved_Event_IsNotRaisedOnFailedDelete()
    {
        List<int> bodyIdsRemoved = [];
        _sim.BodyRemoved += bodyIdsRemoved.Add;

        for (int i = 0; i < 3; i++) _sim.TryAddBody(new CelestialBody(i));

        _sim.TryDeleteBody(3);

        Assert.That(bodyIdsRemoved, Is.Empty);
    }

    #endregion

    // Step function tests in separate test fixture
}