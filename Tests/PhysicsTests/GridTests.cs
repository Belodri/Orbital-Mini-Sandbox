using Physics;
using Physics.Bodies;
using Physics.Core;
using Physics.Models;

namespace PhysicsTests;

[TestFixture]
[DefaultFloatingPointTolerance(1e-12)]
internal class GridTests
{
    public Grid grid = null!;
    public List<CelestialBody> bodies = null!;

    CelestialBody AddBody(PresetBodyData preset)
    {
        var body = CelestialBody.Create(preset);
        bodies.Add(body);
        return body;
    }

    CelestialBody AddBody(int Id, bool Enabled, double Mass, double PosX, double PosY, double VelX, double VelY)
    {
        PresetBodyData preset = new(Id, Enabled, Mass, PosX, PosY, VelX, VelY);
        return AddBody(preset);
    }

    [SetUp]
    public void BeforeEachTest()
    {
        grid = new();
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
        var body = AddBody(1, true, 100, 50, -50, 0, 0);

        // Act
        grid.Rebuild(bodies);

        // Assert
        Assert.That(grid.Root, Is.Not.Null, "Root should be created for a single body.");

        // A single point has zero width and height, so padding is only the flat amount.
        double expectedPadding = Grid.PADDING_FLAT;
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
        AddBody(1, true, 10, -10, -10, 0, 0); // SW
        AddBody(2, true, 10,  10, -10, 0, 0); // SE
        AddBody(3, true, 10, -10,  10, 0, 0); // NW
        AddBody(4, true, 10,  10,  10, 0, 0); // NE

        // Act
        grid.Rebuild(bodies);

        // Assert
        Assert.That(grid.Root, Is.Not.Null, "Root should not be null.");

        // Manually calculate the expected bounds for verification.
        double width = 20; // maxX(10) - minX(-10)
        double height = 20; // maxY(10) - minY(-10)
        double padding = Math.Max(width, height) * Grid.PADDING_MULT + Grid.PADDING_FLAT;
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
