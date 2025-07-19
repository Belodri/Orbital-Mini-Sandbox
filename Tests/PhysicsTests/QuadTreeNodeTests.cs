using Physics;
using Physics.Bodies;
using Physics.Core;
using Physics.Models;
using System.Reflection;
using static PhysicsTests.TestHelpers;

namespace PhysicsTests;

[TestFixture]
[DefaultFloatingPointTolerance(1e-12)]
internal class QuadTreeNodeTests
{
    #region Helpers

    // A standard boundary used for most tests. A 200x200 box centered at (0,0).
    private readonly AABB _standardBounds = new(Vector2D.Zero, new Vector2D(100, 100));

    private readonly ICelestialBody bodyInBounds = new CelestialBody(1, true, 100, new(50, 50));
    private readonly ICelestialBody bodyOutOfBounds = new CelestialBody(2, true, 200, new(200, 200));

    #endregion

    private IQuadTreeNode _root = null!;

    [SetUp]
    public void BeforeEachTest()
    {
        _root = new QuadTreeNode(_standardBounds);
    }


    #region Insert Tests

    [Test]
    public void Insert_BodyOutsideBounds_ReturnsFalseAndDoesNotModifyNode()
    {
        // Arrange
        var body = bodyOutOfBounds; // Position is outside the bounds

        // Act
        bool result = _root.Insert(body);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.False, "Insert should return false for a body outside the bounds.");
            Assert.That(_root.Count, Is.EqualTo(0), "Node count should remain 0.");
            Assert.That(_root.IsExternal, Is.True, "Node should remain external.");
        });
    }

    [Test]
    public void Insert_FirstBodyIntoEmptyNode_StoresBodyAndRemainsExternal()
    {
        // Arrange
        var body = bodyInBounds;

        // Act
        bool result = _root.Insert(body);

        // Assert: We check the public state of the node.
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.True);
            Assert.That(_root.Count, Is.EqualTo(1));
            Assert.That(_root.IsExternal, Is.True, "Node with one body should be external.");
        });
    }

    [Test]
    public void Insert_SecondBody_CausesSubdivisionAndBecomesInternal()
    {
        // Arrange
        var body1 = new CelestialBody(id: 1, mass: 100, position: new(-10, -10)); 
        var body2 = new CelestialBody(id: 2, mass: 200, position: new(10, 10));

        Assert.That(GetPrivateField<CelestialBody>(_root, "_body"), Is.Null);

        // Act
        _root.Insert(body1);

        Assert.That(GetPrivateField<CelestialBody>(_root, "_body"), Is.Not.Null);

        _root.Insert(body2);


        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(_root.Count, Is.EqualTo(2), "Count should be 2 after inserting two bodies.");
            Assert.That(_root.IsExternal, Is.False, "Node should become internal after second insert.");
            // Verify that the original body was cleared from the parent node after subdivision.
            Assert.That(GetPrivateField<CelestialBody>(_root, "_body"), Is.Null);
        });
    }

    [Test]
    public void Insert_FourBodies_DistributesToCorrectQuadrants()
    {
        // Arrange
        var bodyNW = new CelestialBody(id: 0, mass: 10, position: new(-50, 50));  // North-West
        var bodyNE = new CelestialBody(id: 1, mass: 10, position: new(50, 50));   // North-East
        var bodySW = new CelestialBody(id: 2, mass: 10, position: new(-50, -50)); // South-West
        var bodySE = new CelestialBody(id: 3, mass: 10, position: new(50, -50));  // South-East
        
        // Act
        _root.Insert(bodyNW);
        _root.Insert(bodyNE);
        _root.Insert(bodySW);
        _root.Insert(bodySE);

        // Assert
        var nwChild = GetPrivateField<QuadTreeNode>(_root, "_nwChild");
        var neChild = GetPrivateField<QuadTreeNode>(_root, "_neChild");
        var swChild = GetPrivateField<QuadTreeNode>(_root, "_swChild");
        var seChild = GetPrivateField<QuadTreeNode>(_root, "_seChild");

        Assert.Multiple(() =>
        {
            Assert.That(nwChild, Is.Not.Null);
            Assert.That(neChild, Is.Not.Null);
            Assert.That(swChild, Is.Not.Null);
            Assert.That(seChild, Is.Not.Null);
        });

        Assert.Multiple(() =>
        {
            Assert.That(_root.Count, Is.EqualTo(4));
            Assert.That(nwChild.Count, Is.EqualTo(1), "NW child should contain one body.");
            Assert.That(neChild.Count, Is.EqualTo(1), "NE child should contain one body.");
            Assert.That(swChild.Count, Is.EqualTo(1), "SW child should contain one body.");
            Assert.That(seChild.Count, Is.EqualTo(1), "SE child should contain one body.");
        });
    }

    [Test]
    public void Insert_MultipleCoincidentBodies_ForcesMaxDepthAndCrowding()
    {
        // Arrange
        var body1 = new CelestialBody(id: 1, mass: 10, position: new(1.23, 4.56));
        var body2 = new CelestialBody(id: 2, mass: 20, position: new(1.23, 4.56));
        var body3 = new CelestialBody(id: 3, mass: 30, position: new(1.23, 4.56));

        // Act
        _root.Insert(body1);
        _root.Insert(body2);
        _root.Insert(body3);

        // Assert
        // We need to traverse the tree to find the crowded node.
        IQuadTreeNode currentNode = _root;
        int depth = 0;
        while (!currentNode.IsExternal && depth <= 32) // 32 is MAX_DEPTH
        {
            depth++;
            QuadTreeNode? child;

            // All bodies are in the same quadrant, so only one child path will have Count > 0
            child = GetPrivateField<QuadTreeNode>(currentNode, "_nwChild");
            if (child != null && child.Count > 0)
            {
                currentNode = child;
                continue;
            }

            child = GetPrivateField<QuadTreeNode>(currentNode, "_neChild");
            if (child != null && child.Count > 0)
            {
                currentNode = child;
                continue;
            }

            child = GetPrivateField<QuadTreeNode>(currentNode, "_swChild");
            if (child != null && child.Count > 0)
            {
                currentNode = child;
                continue;
            }

            child = GetPrivateField<QuadTreeNode>(currentNode, "_seChild");
            if (child != null && child.Count > 0)
            {
                currentNode = child;
                continue;
            }
        }

        Assert.Multiple(() =>
        {
            Assert.That(_root.Count, Is.EqualTo(3));
            Assert.That(depth, Is.EqualTo(32), "Tree should have subdivided to MAX_DEPTH.");
            Assert.That(currentNode.IsExternal, Is.True, "Node at MAX_DEPTH should be external.");

            var bodyCount = 0;
            foreach (var body in currentNode.Bodies) bodyCount++;
            Assert.That(bodyCount, Is.EqualTo(3), "Node should contain 3 bodies.");
        });
    }

    [Test]
    public void Insert_BodyExactlyOnBoundary_IsPlacedInOneChildWithoutError()
    {
        // Arrange
        // Center (0,0), HalfDim (100,100)
        var bodyOnXAxis = new CelestialBody(id: 1, mass: 10, position: new(0, 50)); // On boundary between NW and NE
        var bodyOnYAxis = new CelestialBody(id: 2, mass: 10, position: new(50, 0)); // On boundary between NE and SE
        var bodyOnCenter = new CelestialBody(id: 3, mass: 10, position: new(0, 0));  // On boundary of all four

        // Act & Assert
        Assert.DoesNotThrow(() => {
            _root.Insert(bodyOnXAxis);
            _root.Insert(bodyOnYAxis);
            _root.Insert(bodyOnCenter);
        }, "Inserting a body on a boundary should not throw an exception.");
        
        // We don't care *which* quadrant it goes into, only that it is successfully placed.
        Assert.That(_root.Count, Is.EqualTo(3), "All three bodies should be inserted.");
    }

    #endregion


    #region Evaluate Tests

    [Test]
    public void Evaluate_OnExternalNode_MatchesSingleBodyProperties()
    {
        // Arrange
        var body = new CelestialBody(id: 1, mass: 150, position: new(25, -75));
        _root.Insert(body);

        // Act
        _root.Evaluate();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(_root.TotalMass, Is.EqualTo(body.Mass));
            Assert.That(_root.CenterOfMass, Is.EqualTo(body.Position));
        });
    }

    [Test]
    public void Evaluate_OnInternalNode_AggregatesChildProperties()
    {
        // Arrange
        var body1 = new CelestialBody(id: 1, mass: 10, position: new(-10, 0));
        var body2 = new CelestialBody(id: 2, mass: 30, position: new(10, 0));
        _root.Insert(body1);
        _root.Insert(body2);

        // Act
        _root.Evaluate();

        // Assert
        double expectedTotalMass = 40.0;
        // Weighted average: ((-10 * 10) + (10 * 30)) / (10 + 30) = (-100 + 300) / 40 = 200 / 40 = 5
        var expectedCoM = new Vector2D(5, 0);

        Assert.Multiple(() =>
        {
            Assert.That(_root.TotalMass, Is.EqualTo(expectedTotalMass));
            Assert.That(_root.CenterOfMass, Is.EqualTo(expectedCoM));
        });
    }

    [Test]
    public void Evaluate_WithZeroMassBodies_HandlesCorrectlyWithoutError()
    {
        // Arrange
        var body1 = new CelestialBody(id: 1, mass: 10, position: new(-10, 0)); // Positive mass
        var body2 = new CelestialBody(id: 2, mass: 00, position: new(00, 0));   // Zero mass
        var body3 = new CelestialBody(id: 3, mass: -10, position: new(10, 0));  // Negative mass
        _root.Insert(body1);
        _root.Insert(body2);
        _root.Insert(body3);
        
        // Act
        _root.Evaluate();

        // Assert
        // TotalMass = 10 + 0 + (-10) = 0
        // Because TotalMass is 0, CenterOfMass should remain at its default.
        Assert.Multiple(() =>
        {
            Assert.That(_root.TotalMass, Is.EqualTo(0));
            Assert.That(_root.CenterOfMass, Is.EqualTo(Vector2D.Zero));
        });
    }


    [Test]
    public void Evaluate_OnEmptyTree_ResultsInZeroValues()
    {
        // Act
        _root.Evaluate();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(_root.Count, Is.EqualTo(0));
            Assert.That(_root.TotalMass, Is.EqualTo(0));
            Assert.That(_root.CenterOfMass, Is.EqualTo(Vector2D.Zero));
        });
    }

    [Test]
    public void Evaluate_InternalNodeWithEmptyChildren_CalculatesCorrectly()
    {
        // Arrange
        var bodyNW = new CelestialBody(id: 1, mass: 10, position: new(-50, 50));    // Will go to NW child
        var bodySE = new CelestialBody(id: 2, mass: 30, position: new(50, -50));    // Will go to SE child
                                                    // NE and SW children will be empty.
        _root.Insert(bodyNW);
        _root.Insert(bodySE);

        // Act
        _root.Evaluate();

        // Assert
        double expectedTotalMass = 40.0;
        // Weighted avg: ((-50,50)*10 + (50,-50)*30) / 40 = ((-500,500) + (1500,-1500)) / 40
        // = (1000, -1000) / 40 = (25, -25)
        var expectedCoM = new Vector2D(25, -25);

        Assert.Multiple(() =>
        {
            Assert.That(_root.TotalMass, Is.EqualTo(expectedTotalMass));
            Assert.That(_root.CenterOfMass.X, Is.EqualTo(expectedCoM.X));
            Assert.That(_root.CenterOfMass.Y, Is.EqualTo(expectedCoM.Y));
        });
    }
    #endregion


    #region MAX_DEPTH Edge Case Tests

    [Test]
    public void Insert_AtMaxDepth_DoesNotSubdivideAndBecomesCrowded()
    {
        // Arrange
        // Use reflection to call the private constructor to create a node at max depth.
        var node = (QuadTreeNode)Activator.CreateInstance(
            typeof(QuadTreeNode),
            BindingFlags.NonPublic | BindingFlags.Instance,
            null,
            [_standardBounds, 32], // 32 is MAX_DEPTH
            null)!;

        var body1 = new CelestialBody(id: 1, mass: 10, position: new(1, 1));
        var body2 = new CelestialBody(id: 2, mass: 10, position: new(2, 2));

        // Act
        node.Insert(body1);
        node.Insert(body2);

        // Assert

        // Check internal state to confirm it's using the crowded list.
        var crowdedBodies = GetPrivateField<List<ICelestialBody>>(node, "_crowdedBodies");
        Assert.That(crowdedBodies, Is.Not.Null);

        Assert.Multiple(() =>
        {
            Assert.That(node.IsExternal, Is.True, "Node should remain external at MAX_DEPTH.");
            Assert.That(node.Count, Is.EqualTo(2));
            Assert.That(crowdedBodies, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public void Evaluate_OnCrowdedNode_CalculatesCorrectly()
    {
        // Arrange
        var node = (QuadTreeNode)Activator.CreateInstance(
            typeof(QuadTreeNode),
            BindingFlags.NonPublic | BindingFlags.Instance,
            null,
            [_standardBounds, 32],
            null)!;

        var body1 = new CelestialBody(id: 1, mass: 10, position: new(-10, 0));
        var body2 = new CelestialBody(id: 2, mass: 30, position: new(10, 0));
        node.Insert(body1);
        node.Insert(body2);
        
        // Act
        node.Evaluate();

        // Assert (Same expected results as the internal node test, but via a different code path)
        double expectedTotalMass = 40.0;
        var expectedCoM = new Vector2D(5, 0);
        Assert.Multiple(() =>
        {
            Assert.That(node.TotalMass, Is.EqualTo(expectedTotalMass));
            Assert.That(node.CenterOfMass, Is.EqualTo(expectedCoM));
        });
    }

    #endregion
}