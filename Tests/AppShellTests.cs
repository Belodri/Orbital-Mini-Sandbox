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
}