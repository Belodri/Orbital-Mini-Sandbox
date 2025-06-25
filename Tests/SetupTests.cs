using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace Tests;

[TestFixture]
public partial class Tests : PageTest
{
    public string BaseUrl = "http://localhost:8080";

    [OneTimeSetUp]
    public void SetBaseUrl()
    {
        var urlParam = TestContext.Parameters["BaseUrl"];
        if (!string.IsNullOrEmpty(urlParam)) BaseUrl = urlParam;
    }

    [SetUp]
    public async Task RunSetup()
    {
        await Page.GotoAsync(BaseUrl);
        await Page.WaitForFunctionAsync("() => typeof window.AppShell === 'function'", null, new() { Timeout = 15000 });
    }
}