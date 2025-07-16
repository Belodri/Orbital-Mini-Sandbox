using Physics;
using Physics.Bodies;
using Physics.Core;
using Physics.Models;
using System.Reflection;

namespace PhysicsTests;

[TestFixture]
[DefaultFloatingPointTolerance(1e-12)]
internal class QuadTreeNodeTests
{
    #region Helpers

    /// <summary>
    /// Uses reflection to get the value of a private field from an object.
    /// This is useful in unit testing to verify internal state without
    /// exposing private fields in the public API.
    /// </summary>
    private static T? GetPrivateField<T>(object obj, string fieldName) where T : class
    {
        var field = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        if (field == null)
        {
            Assert.Fail($"Private field '{fieldName}' not found on type '{obj.GetType().Name}'.");
            return null; // Will not be reached due to Assert.Fail
        }
        return field.GetValue(obj) as T;
    }


    // A simplified helper for tests where enabled status and velocity are not relevant.
    private static CelestialBody CreateBody(int id, double mass, double posX, double posY)
    {
        BodyData preset = new(id, true, mass, new(posX, posY), Vector2D.Zero);
        return CreateBody(preset);
    }

    static CelestialBody CreateBody(BodyData preset)
    {
        return new(preset);
    }

    static CelestialBody CreateBody(int Id, bool Enabled, double Mass, double PosX, double PosY, double VelX, double VelY)
    {
        BodyData preset = new(Id, Enabled, Mass, new(PosX, PosY), new(VelX, VelY));
        return CreateBody(preset);
    }

    // A standard boundary used for most tests. A 200x200 box centered at (0,0).
    private readonly AABB _standardBounds = new(Vector2D.Zero, new Vector2D(100, 100));

    #endregion


    #region Insert Tests

    [Test]
    public void Insert_BodyOutsideBounds_ReturnsFalseAndDoesNotModifyNode()
    {
        // Arrange
        var root = new QuadTreeNode(_standardBounds);
        var body = CreateBody(1, 100, 200, 200); // Position is outside the bounds

        // Act
        bool result = root.Insert(body);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.False, "Insert should return false for a body outside the bounds.");
            Assert.That(root.Count, Is.EqualTo(0), "Node count should remain 0.");
            Assert.That(root.IsExternal, Is.True, "Node should remain external.");
        });
    }

    [Test]
    public void Insert_FirstBodyIntoEmptyNode_StoresBodyAndRemainsExternal()
    {
        // Arrange
        var root = new QuadTreeNode(_standardBounds);
        var body = CreateBody(1, 100, 10, 10);

        // Act
        bool result = root.Insert(body);

        // Assert: We check the public state of the node.
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.True);
            Assert.That(root.Count, Is.EqualTo(1));
            Assert.That(root.IsExternal, Is.True, "Node with one body should be external.");
        });
    }

    [Test]
    public void Insert_SecondBody_CausesSubdivisionAndBecomesInternal()
    {
        // Arrange
        var root = new QuadTreeNode(_standardBounds);
        var body1 = CreateBody(1, 100, -10, -10);
        var body2 = CreateBody(2, 100, 10, 10);

        // Act
        root.Insert(body1);
        root.Insert(body2);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(root.Count, Is.EqualTo(2), "Count should be 2 after inserting two bodies.");
            Assert.That(root.IsExternal, Is.False, "Node should become internal after second insert.");
            // Verify that the original body was cleared from the parent node after subdivision.
            Assert.That(GetPrivateField<CelestialBody>(root, "_body"), Is.Null);
        });
    }

    [Test]
    public void Insert_FourBodies_DistributesToCorrectQuadrants()
    {
        // Arrange
        var root = new QuadTreeNode(_standardBounds);
        var bodyNW = CreateBody(1, 10, -50, 50);  // North-West
        var bodyNE = CreateBody(2, 10, 50, 50);   // North-East
        var bodySW = CreateBody(3, 10, -50, -50); // South-West
        var bodySE = CreateBody(4, 10, 50, -50);  // South-East
        
        // Act
        root.Insert(bodyNW);
        root.Insert(bodyNE);
        root.Insert(bodySW);
        root.Insert(bodySE);

        // Assert
        var nwChild = GetPrivateField<QuadTreeNode>(root, "_nwChild");
        var neChild = GetPrivateField<QuadTreeNode>(root, "_neChild");
        var swChild = GetPrivateField<QuadTreeNode>(root, "_swChild");
        var seChild = GetPrivateField<QuadTreeNode>(root, "_seChild");

        Assert.Multiple(() =>
        {
            Assert.That(nwChild, Is.Not.Null);
            Assert.That(neChild, Is.Not.Null);
            Assert.That(swChild, Is.Not.Null);
            Assert.That(seChild, Is.Not.Null);
        });

        Assert.Multiple(() =>
        {
            Assert.That(root.Count, Is.EqualTo(4));
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
        var root = new QuadTreeNode(_standardBounds);
        var body1 = CreateBody(1, 10, 1.23, 4.56);
        var body2 = CreateBody(2, 20, 1.23, 4.56);
        var body3 = CreateBody(3, 30, 1.23, 4.56);

        // Act
        root.Insert(body1);
        root.Insert(body2);
        root.Insert(body3);

        // Assert
        // We need to traverse the tree to find the crowded node.
        QuadTreeNode currentNode = root;
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

        var crowdedBodies = GetPrivateField<List<CelestialBody>>(currentNode, "_crowdedBodies");

        Assert.Multiple(() =>
        {
            Assert.That(root.Count, Is.EqualTo(3));
            Assert.That(depth, Is.EqualTo(32), "Tree should have subdivided to MAX_DEPTH.");
            Assert.That(currentNode.IsExternal, Is.True, "Node at MAX_DEPTH should be external.");
            Assert.That(crowdedBodies, Is.Not.Null);
            Assert.That(crowdedBodies, Has.Count.EqualTo(3));
        });
    }

    [Test]
    public void Insert_BodyExactlyOnBoundary_IsPlacedInOneChildWithoutError()
    {
        // Arrange
        var root = new QuadTreeNode(_standardBounds); // Center (0,0), HalfDim (100,100)
        var bodyOnXAxis = CreateBody(1, 10, 0, 50); // On boundary between NW and NE
        var bodyOnYAxis = CreateBody(2, 10, 50, 0); // On boundary between NE and SE
        var bodyOnCenter = CreateBody(3, 10, 0, 0);  // On boundary of all four

        // Act & Assert
        Assert.DoesNotThrow(() => {
            root.Insert(bodyOnXAxis);
            root.Insert(bodyOnYAxis);
            root.Insert(bodyOnCenter);
        }, "Inserting a body on a boundary should not throw an exception.");
        
        // We don't care *which* quadrant it goes into, only that it is successfully placed.
        Assert.That(root.Count, Is.EqualTo(3), "All three bodies should be inserted.");
    }

    #endregion


    #region Evaluate Tests

    [Test]
    public void Evaluate_OnExternalNode_MatchesSingleBodyProperties()
    {
        // Arrange
        var root = new QuadTreeNode(_standardBounds);
        var body = CreateBody(1, 150, 25, -75);
        root.Insert(body);

        // Act
        root.Evaluate();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(root.TotalMass, Is.EqualTo(body.Mass));
            Assert.That(root.CenterOfMass, Is.EqualTo(body.Position));
        });
    }

    [Test]
    public void Evaluate_OnInternalNode_AggregatesChildProperties()
    {
        // Arrange
        var root = new QuadTreeNode(_standardBounds);
        var body1 = CreateBody(1, 10, -10, 0);
        var body2 = CreateBody(2, 30, 10, 0);
        root.Insert(body1);
        root.Insert(body2);

        // Act
        root.Evaluate();

        // Assert
        double expectedTotalMass = 40.0;
        // Weighted average: ((-10 * 10) + (10 * 30)) / (10 + 30) = (-100 + 300) / 40 = 200 / 40 = 5
        var expectedCoM = new Vector2D(5, 0);

        Assert.Multiple(() =>
        {
            Assert.That(root.TotalMass, Is.EqualTo(expectedTotalMass));
            Assert.That(root.CenterOfMass, Is.EqualTo(expectedCoM));
        });
    }

    [Test]
    public void Evaluate_WithZeroMassBodies_HandlesCorrectlyWithoutError()
    {
        // Arrange
        var root = new QuadTreeNode(_standardBounds);
        var body1 = CreateBody(1, 10, -10, 0); // Positive mass
        var body2 = CreateBody(2, 0, 0, 0);   // Zero mass
        var body3 = CreateBody(3, -10, 10, 0);  // Negative mass
        root.Insert(body1);
        root.Insert(body2);
        root.Insert(body3);
        
        // Act
        root.Evaluate();

        // Assert
        // TotalMass = 10 + 0 + (-10) = 0
        // Because TotalMass is 0, CenterOfMass should remain at its default.
        Assert.Multiple(() =>
        {
            Assert.That(root.TotalMass, Is.EqualTo(0));
            Assert.That(root.CenterOfMass, Is.EqualTo(Vector2D.Zero));
        });
    }


    [Test]
    public void Evaluate_OnEmptyTree_ResultsInZeroValues()
    {
        // Arrange
        var root = new QuadTreeNode(_standardBounds);

        // Act
        root.Evaluate();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(root.Count, Is.EqualTo(0));
            Assert.That(root.TotalMass, Is.EqualTo(0));
            Assert.That(root.CenterOfMass, Is.EqualTo(Vector2D.Zero));
        });
    }

    [Test]
    public void Evaluate_InternalNodeWithEmptyChildren_CalculatesCorrectly()
    {
        // Arrange
        var root = new QuadTreeNode(_standardBounds);
        var bodyNW = CreateBody(1, 10, -50, 50);    // Will go to NW child
        var bodySE = CreateBody(2, 30, 50, -50);    // Will go to SE child
                                                    // NE and SW children will be empty.
        root.Insert(bodyNW);
        root.Insert(bodySE);

        // Act
        root.Evaluate();

        // Assert
        double expectedTotalMass = 40.0;
        // Weighted avg: ((-50,50)*10 + (50,-50)*30) / 40 = ((-500,500) + (1500,-1500)) / 40
        // = (1000, -1000) / 40 = (25, -25)
        var expectedCoM = new Vector2D(25, -25);

        Assert.Multiple(() =>
        {
            Assert.That(root.TotalMass, Is.EqualTo(expectedTotalMass));
            Assert.That(root.CenterOfMass.X, Is.EqualTo(expectedCoM.X));
            Assert.That(root.CenterOfMass.Y, Is.EqualTo(expectedCoM.Y));
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

        var body1 = CreateBody(1, 10, 1, 1);
        var body2 = CreateBody(2, 10, 2, 2);

        // Act
        node.Insert(body1);
        node.Insert(body2);

        // Assert

        // Check internal state to confirm it's using the crowded list.
        var crowdedBodies = GetPrivateField<List<CelestialBody>>(node, "_crowdedBodies");
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

        var body1 = CreateBody(1, 10, -10, 0);
        var body2 = CreateBody(2, 30, 10, 0);
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