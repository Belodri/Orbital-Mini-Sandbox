using System.Data.Common;
using System.Linq.Expressions;
using Physics;
using Physics.Bodies;
using Physics.Core;
using Physics.Models;
using static PhysicsTests.TestHelpers;

namespace PhysicsTests;

[TestFixture]
[DefaultFloatingPointTolerance(1e-12)]
internal class GridTests
{
    public IGrid grid = null!;
    public List<ICelestialBody> bodies = null!;

    ICelestialBody AddBody(ICelestialBody body)
    {
        bodies.Add(body);
        return body;
    }

    [SetUp]
    public void BeforeEachTest()
    {
        grid = new Grid();
        bodies = [];
    }

    [Test]
    public void Rebuild_WithEmptyList_SetsNullRootAndZeroBounds()
    {
        // Act
        grid.Rebuild(bodies);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(grid.Root, Is.Null, "Root should be null for an empty body list.");
            Assert.That(grid.OuterBounds, Is.EqualTo(new AABB(Vector2D.Zero, Vector2D.Zero)), "Grid.OuterBounds should be a zero-AABB for an empty body list.");
        });
    }

    [Test]
    public void Rebuild_WithSingleBody_CorrectlyInitializesRootAndBounds()
    {
        // Arrange
        var body = AddBody(new CelestialBody(id: 1, enabled: true, mass: 1e6, position: new(2,2)));

        // Act
        grid.Rebuild(bodies);

        // Assert
        Assert.That(grid.Root, Is.Not.Null, "Root should be created for a single body.");

        // A single point has zero width and height, so padding is only the flat amount.
        double expectedPadding = Grid.TEST_PADDING_FLAT;
        var expectedHalfDim = new Vector2D(expectedPadding, expectedPadding);

        Assert.Multiple(() =>
        {
            // --- Check Grid properties ---
            Assert.That(grid.OuterBounds.Center, Is.EqualTo(body.Position), "OuterBounds.Center should match the body's position.");
            Assert.That(grid.OuterBounds.HalfDimension, Is.EqualTo(expectedHalfDim), "OuterBounds.HalfDimension should only have flat padding.");

            // --- Check Root Node properties (using ! because we've asserted Not.Null) ---
            Assert.That(grid.Root.IsExternal, Is.True, "Root with one body should be an external node.");
            Assert.That(grid.Root.Count, Is.EqualTo(1), "Count should be 1.");
            Assert.That(grid.Root.TotalMass, Is.EqualTo(body.Mass), "TotalMass should match the body's mass.");
            Assert.That(grid.Root.CenterOfMass, Is.EqualTo(body.Position), "CenterOfMass should match the body's position.");
        });
    }

    [Test]
    public void Rebuild_WithMultipleBodies_CorrectlyCalculatesBoundsAndMassDistribution()
    {
        // Arrange: Create a symmetrical system for easy-to-predict results.
        AddBody(new CelestialBody(id: 1, enabled: true, mass: 10, position: new(-10,-10))); // SW
        AddBody(new CelestialBody(id: 2, enabled: true, mass: 10, position: new(10,-10))); // SE
        AddBody(new CelestialBody(id: 3, enabled: true, mass: 10, position: new(-10, 10))); // NW
        AddBody(new CelestialBody(id: 4, enabled: true, mass: 10, position: new(10, 10))); // NE

        // Act
        grid.Rebuild(bodies);

        // Assert
        Assert.That(grid.Root, Is.Not.Null, "Root should not be null.");

        // Manually calculate the expected bounds for verification.
        double width = 20; // maxX(10) - minX(-10)
        double height = 20; // maxY(10) - minY(-10)
        double padding = Math.Max(width, height) * Grid.TEST_PADDING_MULT + Grid.TEST_PADDING_FLAT;
        Vector2D expectedHalfDim = new(width / 2.0 + padding, height / 2.0 + padding);
        var expectedCenter = Vector2D.Zero;

        double totalMass = bodies.Sum(b => b.Mass);

        Assert.Multiple(() =>
        {
            // --- Check Grid properties ---
            Assert.That(grid.OuterBounds.Center, Is.EqualTo(expectedCenter), "Center of a symmetrical system should be at the origin.");
            Assert.That(grid.OuterBounds.HalfDimension, Is.EqualTo(expectedHalfDim), "Half-dimension calculation should be correct including padding.");

            // --- Check Root Node properties ---
            Assert.That(grid.Root.IsExternal, Is.False, "Root should subdivide and become an internal node.");
            Assert.That(grid.Root.Count, Is.EqualTo(4), "Count should include all four bodies.");
            Assert.That(grid.Root.TotalMass, Is.EqualTo(totalMass), "TotalMass should be the sum of all body masses.");
            Assert.That(grid.Root.CenterOfMass, Is.EqualTo(expectedCenter), "Center of mass for a symmetrical system should be at the origin.");
        });
    }
}
