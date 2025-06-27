using Physics;

namespace PhysicsTests;

[TestFixture]
public partial class Tests
{
    public PhysicsEngine physicsEngine = null!;

    [SetUp]
    public void ReplaceEngine()
    {
        physicsEngine = new();
    }
}
