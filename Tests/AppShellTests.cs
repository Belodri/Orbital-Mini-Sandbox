using NUnit.Framework;

namespace Tests;

[TestFixture]
public partial class Tests
{
    // Default [OneTimeSetUp] and [SetUp] methods are in SetupTests.cs

    [Test(Description = "Confirms that the AppShell global object is created and is a function.")]
    public async Task AppShell_ShouldBeDefinedOnGlobalScope()
    {
        var appShellType = await Page.EvaluateAsync<string>("() => typeof window.AppShell");
        Assert.That(appShellType, Is.EqualTo("function"), "The global AppShell should be of type 'function'.");
    }

    [Test(Description = "Verifies the full lifecycle: creating a body and then successfully destroying it using its id.")]
    public async Task CreateAndDestroyBody_WithValidId_ShouldSucceed()
    {
        var bodyId = await Page.EvaluateAsync<int>("() => window.AppShell.createBody()");
        Assert.That(bodyId, Is.GreaterThan(0), "Pre-condition failed: A valid id was not created.");

        var result = await Page.EvaluateAsync<bool>("(id) => window.AppShell.deleteBody(id)", bodyId);
        Assert.That(result, Is.True, "deleteBody(id) with a valid id should return true.");
    }

    [Test(Description = "Verifies that AppShell.createBody() returns a positive integer.")]
    public async Task CreateBody_ShouldReturnPositiveInteger()
    {
        var bodyId = await Page.EvaluateAsync<int>("() => window.AppShell.createBody()");
        Assert.That(bodyId, Is.GreaterThan(0), "createBody() should return a positive integer ID.");
    }

    [Test(Description = "Verifies that AppShell.deleteBody() returns false when passed an invalid id.")]
    public async Task DestroyBody_WithInvalidId_ShouldReturnFalse()
    {
        var result = await Page.EvaluateAsync<bool>("(id) => window.AppShell.deleteBody(id)", -1);
        Assert.That(result, Is.False, "deleteBody() with an invalid ID (-1) should return false.");
    }

    [Test(Description = "Verifies that AppShell.Bridge.tickEngine() returns an object with the expected shape: { created: Set, deleted: Set, updated: Set }.")]
    public async Task TickEngine_ShouldReturnCorrectlyShapedObject()
    {
        var isShapeCorrect = await Page.EvaluateAsync<bool>(
            """
            (dt) => {
                const result = window.AppShell.Bridge.tickEngine(dt);
                if (typeof result !== 'object' || result === null) return false;

                const hasCreated = result.hasOwnProperty('created') && result.created instanceof Set;
                const hasDeleted = result.hasOwnProperty('deleted') && result.deleted instanceof Set;
                const hasUpdated = result.hasOwnProperty('updated') && result.updated instanceof Set;

                return hasCreated && hasDeleted && hasUpdated;
            }
            """, 1);

        Assert.That(isShapeCorrect, Is.True, "tickEngine() did not return an object with created, deleted, and updated properties of type Set.");
    }

    [Test(Description = "Verifies that AppShell.Bridge.simState.bodies exists and is an empty Map on initialization.")]
    public async Task SimState_Bodies_ShouldBeAnEmptyMapInitially()
    {
        var isValid = await Page.EvaluateAsync<bool>(
            """
            () => {
                const simState = window.AppShell?.Bridge?.simState;
                return simState
                    && simState.hasOwnProperty('bodies')
                    && simState.bodies instanceof Map
                    && simState.bodies.size === 0;
            }
            """);

        Assert.That(isValid, Is.True, "Validation failed: simState.bodies should be an empty Map.");
    }

    [Test(Description = "Verifies that AppShell.Bridge.simState.bodyCount is the number 0 on initialization.")]
    public async Task SimState_BodyCount_ShouldBeZeroInitially()
    {
        var isValid = await Page.EvaluateAsync<bool>(
            """
            () => {
                const simState = window.AppShell?.Bridge?.simState;
                return simState
                    && simState.hasOwnProperty('bodyCount')
                    && typeof simState.bodyCount === 'number'
                    && simState.bodyCount === 0;
            }
            """);

        Assert.That(isValid, Is.True, "Validation failed: simState.bodyCount should be the number 0.");
    }

    [Test(Description = "Verifies that AppShell.appDataManager.bodyData is an empty Map on initialization.")]
    public async Task AppDataManager_Bodies_ShouldBeAnEmptyMapInitially()
    {
        var isValid = await Page.EvaluateAsync<bool>(
            """
            () => {
                const manager = window.AppShell?.appDataManager;
                return manager
                    && manager.hasOwnProperty('bodyData')
                    && manager.bodyData instanceof Map
                    && manager.bodyData.size === 0;
            }
            """);

        Assert.That(isValid, Is.True, "Validation failed: appDataManager.bodyData should be an empty Map.");
    }

    [Test(Description = "Verifies that AppShell.appDataManager.bodyData correctly reflects newly created bodyData.")]
    public async Task AppDataManager_Bodies_ShouldContainEntriesAfterCreation()
    {
        var bodyId1 = await Page.EvaluateAsync<int>("() => window.AppShell.createBody()");
        var bodyId2 = await Page.EvaluateAsync<int>("() => window.AppShell.createBody()");

        var isValid = await Page.EvaluateAsync<bool>(
            """
            ([bodyId1, bodyId2]) => {
                const manager = window.AppShell?.appDataManager;
                const bodiesMap = manager?.bodyData;
                return bodiesMap instanceof Map
                    && bodiesMap.size === 2
                    && bodiesMap.has(bodyId1)
                    && bodiesMap.has(bodyId2);
            }
            """, new[] { bodyId1, bodyId2 });

        Assert.That(isValid, Is.True, "Validation failed: appDataManager.bodyData should be a Map with 2 entries containing the new body IDs.");
    }
}