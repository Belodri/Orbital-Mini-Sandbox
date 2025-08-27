using Physics.Bodies;
using Physics.Core;

namespace PhysicsTests;

[TestFixture]
[DefaultFloatingPointTolerance(1e-12)]
public class BodyManagerTests
{
    BodyManager _manager;

    [SetUp]
    public void TestSetup() => _manager = new();

    #region Initialization

    [Test]
    public void Constructor_InitializesWithEmptyCollections()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_manager.BodyCount, Is.Zero);
            Assert.That(_manager.EnabledCount, Is.Zero);
            Assert.That(_manager.AllBodies, Has.Count.Zero);
            Assert.That(_manager.EnabledBodies, Has.Count.Zero);
        });
    }

    #endregion

    #region Body Management (Adding)

    [Test]
    public void CreateBody_WhenCalled_GeneratesUniqueIdAndAddsBody()
    {
        var addedBody = _manager.CreateBody(id => new CelestialBody(id));

        Assert.Multiple(() =>
        {
            Assert.That(addedBody.Id, Is.EqualTo(0));
            Assert.That(_manager.AllBodies, Has.Count.EqualTo(1));
            Assert.That(_manager.GetBodyOrNull(0), Is.EqualTo(addedBody));
        });
    }

    [Test]
    public void CreateBody_WhenCalledMultipleTimes_GeneratesUniqueIds()
    {
        var body0 = _manager.CreateBody(id => new CelestialBody(id));
        var body1 = _manager.CreateBody(id => new CelestialBody(id));

        Assert.That(body0.Id, Is.Not.EqualTo(body1.Id));
    }

    [Test]
    public void TryAddBody_WithNewBody_ReturnsTrueAndAddsBody()
    {
        var body = new CelestialBody(7);
        var returnValue = _manager.TryAddBody(body);

        Assert.Multiple(() =>
        {
            Assert.That(returnValue, Is.True);
            Assert.That(_manager.GetBodyOrNull(7), Is.EqualTo(body));
        });
    }

    [Test]
    public void TryAddBody_WithExistingId_ReturnsFalseAndDoesNotAddBody()
    {
        var body0 = _manager.CreateBody(id => new CelestialBody(id, mass: 1));
        Assert.Multiple(() =>
        {
            Assert.That(_manager.AllBodies, Has.Count.EqualTo(1));
            Assert.That(_manager.GetBodyOrNull(0), Is.EqualTo(body0));
        });

        var returnValue = _manager.TryAddBody(new CelestialBody(0, mass: 5));
        Assert.Multiple(() =>
        {
            Assert.That(returnValue, Is.False);
            Assert.That(_manager.GetBodyOrNull(0), Is.EqualTo(body0));
        });
    }

    #endregion


    #region Body Management (Deleting)

    [Test]
    public void TryDeleteBody_ById_WithExistingId_ReturnsTrueAndRemovesBody()
    {
        var bodyId = _manager.CreateBody(id => new CelestialBody(id)).Id;
        Assert.That(_manager.HasBody(bodyId), Is.True);

        var isDeleted = _manager.TryDeleteBody(bodyId);

        Assert.Multiple(() =>
        {
            Assert.That(isDeleted, Is.True);
            Assert.That(_manager.HasBody(bodyId), Is.False);
        });
    }

    [Test]
    public void TryDeleteBody_ById_WithNonExistentId_ReturnsFalse()
    {
        var bodyId = _manager.CreateBody(id => new CelestialBody(id)).Id;
        Assert.That(_manager.HasBody(bodyId), Is.True);

        int nonExistentId = bodyId + 1;

        var isDeleted = _manager.TryDeleteBody(nonExistentId);
        Assert.Multiple(() =>
        {
            Assert.That(isDeleted, Is.False);
            Assert.That(_manager.HasBody(bodyId), Is.True);
        });
    }

    #endregion


    #region Body Management (Updating)

    [Test]
    public void TryUpdateBody_WithNonExistentId_ReturnsFalse()
    {
        var wasUpdated = _manager.TryUpdateBody(0, new(Mass: 100));

        Assert.That(wasUpdated, Is.False);
    }

    [Test]
    public void TryUpdateBody_WithNonExistentId_DoesNotEvent()
    {
        int eventCount = 0;
        _manager.EnabledContentModified += () => eventCount++;

        _manager.TryUpdateBody(0, new(Enabled: true));

        Assert.That(eventCount, Is.Zero);
    }

    [Test]
    public void TryUpdateBody_WithExistingId_ReturnsTrueAndAppliesUpdates()
    {
        var body = _manager.CreateBody(id => new CelestialBody(id, mass: 10));
        var wasUpdated = _manager.TryUpdateBody(body.Id, new(Mass: 100));

        Assert.Multiple(() =>
        {
            Assert.That(wasUpdated, Is.True);
            Assert.That(body.Mass, Is.EqualTo(100));
        });
    }

    #endregion


    #region Event Notifications (BodyAdded / BodyRemoved)

    [Test]
    public void BodyAdded_Event_OnCreateBody_IsRaisedOnSuccessfulAdd()
    {
        List<int> bodyIdsAdded = [];
        _manager.BodyAdded += body => bodyIdsAdded.Add(body.Id);
        for (int i = 0; i < 3; i++) _manager.CreateBody(id => new CelestialBody(id));

        Assert.That(bodyIdsAdded, Has.Count.EqualTo(3));
        Assert.That(bodyIdsAdded, Is.EqualTo(new List<int>([0, 1, 2])));
    }

    [Test]
    public void BodyAdded_Event_OnTryAddBody_IsRaisedOnSuccessfulAdd()
    {
        List<int> bodyIdsAdded = [];
        _manager.BodyAdded += body => bodyIdsAdded.Add(body.Id);
        for (int i = 0; i < 3; i++) _manager.TryAddBody(new CelestialBody(i));

        Assert.That(bodyIdsAdded, Has.Count.EqualTo(3));
        Assert.That(bodyIdsAdded, Is.EqualTo(new List<int>([0, 1, 2])));
    }

    [Test]
    public void BodyAdded_Event_IsNotRaisedOnFailedAdd()
    {
        // Add blocking body
        _manager.TryAddBody(new CelestialBody(1));

        List<int> bodyIdsAdded = [];
        _manager.BodyAdded += body => bodyIdsAdded.Add(body.Id);

        for (int i = 0; i < 3; i++) _manager.TryAddBody(new CelestialBody(i));

        Assert.That(bodyIdsAdded, Has.Count.EqualTo(2));
        Assert.That(bodyIdsAdded, Is.EqualTo(new List<int>([0, 2])));
    }

    [Test]
    public void BodyRemoved_Event_IsRaisedOnSuccessfulDelete()
    {
        List<int> bodyIdsRemoved = [];
        _manager.BodyRemoved += bodyIdsRemoved.Add;

        for (int i = 0; i < 3; i++) _manager.TryAddBody(new CelestialBody(i));

        _manager.TryDeleteBody(1);
        _manager.TryDeleteBody(2);

        Assert.That(bodyIdsRemoved, Has.Count.EqualTo(2));
        Assert.That(bodyIdsRemoved, Is.EqualTo(new List<int>([1, 2])));
    }

    [Test]
    public void BodyRemoved_Event_IsNotRaisedOnFailedDelete()
    {
        List<int> bodyIdsRemoved = [];
        _manager.BodyRemoved += bodyIdsRemoved.Add;

        for (int i = 0; i < 3; i++) _manager.TryAddBody(new CelestialBody(i));

        _manager.TryDeleteBody(3);

        Assert.That(bodyIdsRemoved, Is.Empty);
    }

    #endregion


    #region Event Notifications (EnabledContentModified)

    [Test]
    public void EnabledContentModified_Event_IsRaised_WhenExpected()
    {
        int counter = 0;
        _manager.EnabledContentModified += () => counter++;

        // 1. Add enabled body => event
        var res = _manager.TryAddBody(new CelestialBody(0, enabled: true));
        Assert.Multiple(() =>
        {
            Assert.That(res, Is.True);
            Assert.That(counter, Is.EqualTo(1));
        });

        // Add disabled body does not raise event
        res = _manager.TryAddBody(new CelestialBody(1));
        Assert.Multiple(() =>
        {
            Assert.That(res, Is.True);
            Assert.That(counter, Is.EqualTo(1));
        });

        // 2. Enable disabled body => event
        res = _manager.TryUpdateBody(1, new(Enabled: true));
        Assert.Multiple(() =>
        {
            Assert.That(res, Is.True);
            Assert.That(counter, Is.EqualTo(2));
        });

        // 3. Delete enabled body => event
        res = _manager.TryDeleteBody(1);
        Assert.Multiple(() =>
        {
            Assert.That(res, Is.True);
            Assert.That(counter, Is.EqualTo(3));
        });

        // 4. Update enabled body => event
        res = _manager.TryUpdateBody(0, new(Mass: 4));
        Assert.Multiple(() =>
        {
            Assert.That(res, Is.True);
            Assert.That(counter, Is.EqualTo(4));
        });

        // 5. Disable enabled body => event
        res = _manager.TryUpdateBody(0, new(Enabled: false));
        Assert.Multiple(() =>
        {
            Assert.That(res, Is.True);
            Assert.That(counter, Is.EqualTo(5));
        });
    }

    [Test]
    public void EnabledContentModified_Event_IsNotRaised_WhenExpected()
    {
        int counter = 0;
        _manager.EnabledContentModified += () => counter++;

        // Add disabled body => no event
        var res = _manager.TryAddBody(new CelestialBody(0));
        Assert.Multiple(() =>
        {
            Assert.That(res, Is.True);
            Assert.That(counter, Is.Zero);
        });

        // Update disabled body => no event
        res = _manager.TryUpdateBody(0, new(Mass: 7));
        Assert.Multiple(() =>
        {
            Assert.That(res, Is.True);
            Assert.That(counter, Is.Zero);
        });

        // Delete disabled body => no event
        _manager.TryDeleteBody(0);
        Assert.Multiple(() =>
        {
            Assert.That(res, Is.True);
            Assert.That(counter, Is.Zero);
        });
    }

    #endregion


    #region Public Collections

    [Test]
    public void AllBodies_And_BodyCount_AreUpdated_OnAddDelete()
    {
        var body0 = _manager.CreateBody(id => new CelestialBody(id));
        var body1 = _manager.CreateBody(id => new CelestialBody(id));

        Assert.Multiple(() =>
        {
            Assert.That(_manager.BodyCount, Is.EqualTo(2));
            Assert.That(_manager.AllBodies, Has.Count.EqualTo(2));
            Assert.That(_manager.AllBodies.ContainsKey(body0.Id), Is.True);
            Assert.That(_manager.AllBodies.ContainsKey(body1.Id), Is.True);
        });

        // Delete one body
        _manager.TryDeleteBody(body0.Id);

        Assert.Multiple(() =>
        {
            Assert.That(_manager.BodyCount, Is.EqualTo(1));
            Assert.That(_manager.AllBodies, Has.Count.EqualTo(1));
            Assert.That(_manager.AllBodies.ContainsKey(body0.Id), Is.False);
            Assert.That(_manager.AllBodies[body1.Id], Is.SameAs(body1));
        });
    }

    [Test]
    public void AllBodies_Values_ContainsAllManagedBodies()
    {
        var body0 = _manager.CreateBody(id => new CelestialBody(id));
        var body1 = _manager.CreateBody(id => new CelestialBody(id));
        _manager.TryAddBody(new CelestialBody(2));
        var body2 = _manager.GetBodyOrNull(2);

        Assert.That(body2, Is.Not.Null);

        List<ICelestialBody> compareList = [body0, body1, body2];
        var allValues = _manager.AllBodies.Values.ToList();

        Assert.That(allValues, Has.Count.EqualTo(3));
        Assert.That(allValues, Is.EquivalentTo(compareList));
    }

    [Test]
    public void EnabledBodies_Contains_AllAndOnlyEnabledBodies()
    {
        // Add 50 bodies to the manager. Those with even ids are enabled
        for (int i = 0; i < 50; i++) _manager.TryAddBody(new CelestialBody(i, enabled: i % 2 == 0));

        Assert.That(_manager.EnabledBodies, Has.Count.EqualTo(25));
        Assert.That(_manager.EnabledBodies.All(b => b.Id % 2 == 0));
    }

    [Test]
    public void EnabledBodies_And_EnabledCount_IsUpdated_OnCreateUpdateDelete()
    {
        void testCount(int count) =>
            Assert.Multiple(() =>
            {
                Assert.That(_manager.EnabledCount, Is.EqualTo(count));
                Assert.That(_manager.EnabledBodies, Has.Count.EqualTo(count));
            });


        // Add disabled body
        _manager.TryAddBody(new CelestialBody(0));
        testCount(0);

        // Add enabled body
        _manager.TryAddBody(new CelestialBody(1, enabled: true));
        testCount(1);

        // Create disabled body
        _manager.CreateBody(id => new CelestialBody(id));
        testCount(1);

        // Create enabled body
        _manager.CreateBody(id => new CelestialBody(id, enabled: true));
        testCount(2);

        // Enable disabled body
        _manager.TryUpdateBody(0, new(Enabled: true));
        testCount(3);

        // Disable enabled body
        _manager.TryUpdateBody(0, new(Enabled: false));
        testCount(2);

        // Delete disabled body
        _manager.TryDeleteBody(0);
        testCount(2);

        // Delete enabled body
        _manager.TryDeleteBody(1);
        testCount(1);
    }

    [Test]
    public void EnabledBodies_ContentIsCorrect_AfterRemovingElement()
    {
        var body0 = _manager.CreateBody(id => new CelestialBody(id, enabled: true));
        var body1 = _manager.CreateBody(id => new CelestialBody(id, enabled: true));
        var body2 = _manager.CreateBody(id => new CelestialBody(id, enabled: true));
        
        Assert.That(_manager.EnabledBodies, Is.EquivalentTo(new[] { body0, body1, body2 }));

        _manager.TryDeleteBody(body1.Id);

        Assert.Multiple(() =>
        {
            Assert.That(_manager.EnabledBodies, Is.EquivalentTo(new[] { body0, body2 }));
            Assert.That(_manager.EnabledCount, Is.EqualTo(2));
        });
    }

    #endregion


    #region Querying

    [Test]
    public void HasBody_WithExistingId_ReturnsTrue()
    {
        var body = _manager.CreateBody(id => new CelestialBody(id));
        Assert.That(_manager.HasBody(body.Id), Is.True);
    }

    [Test]
    public void HasBody_WithNonExistentId_ReturnsFalse()
    {
        Assert.That(_manager.HasBody(0), Is.False);
    }

    [Test]
    public void TryGetBody_WithExistingId_ReturnsTrueAndCorrectBody()
    {
        var expectedBody = _manager.CreateBody(id => new CelestialBody(id));
        var result = _manager.TryGetBody(expectedBody.Id, out var actualBody);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.True);
            Assert.That(actualBody, Is.SameAs(expectedBody));
        });
    }

    [Test]
    public void TryGetBody_WithNonExistentId_ReturnsFalseAndNullBody()
    {
        var result = _manager.TryGetBody(0, out var actualBody);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.False);
            Assert.That(actualBody, Is.Null);
        });
    }

    [Test]
    public void GetBodyOrNull_WithExistingId_ReturnsTheBody()
    {
        var expectedBody = _manager.CreateBody(id => new CelestialBody(id));
        var actualBody = _manager.GetBodyOrNull(expectedBody.Id);

        Assert.That(actualBody, Is.SameAs(expectedBody));
    }

    [Test]
    public void GetBodyOrNull_WithNonExistentId_ReturnsNull()
    {
        var actualBody = _manager.GetBodyOrNull(0);
        Assert.That(actualBody, Is.Null);
    }

    #endregion


    #region White Box Tests

    [Test]
    public void CreateBody_AfterAddingBodyWithSkippedId_GeneratesNextAvailableId()
    {
        _manager.TryAddBody(new CelestialBody(2));
        Assert.That(_manager.HasBody(2), Is.True);

        var body0 = _manager.CreateBody(id => new CelestialBody(id));
        var body1 = _manager.CreateBody(id => new CelestialBody(id));
        var body3 = _manager.CreateBody(id => new CelestialBody(id));

        Assert.Multiple(() =>
        {
            Assert.That(body0.Id, Is.EqualTo(0));
            Assert.That(body1.Id, Is.EqualTo(1));
            Assert.That(body3.Id, Is.EqualTo(3));
        });
    }

    [Test]
    [System.Diagnostics.Conditional("DEBUG")]
    public void CreateBody_WithFactoryThatAssignsWrongId_ThrowsExceptionInDebug()
    {
        Assert.Throws<InvalidOperationException>(() => _manager.CreateBody(id => new CelestialBody(id + 100)));
    }

    #endregion
}
