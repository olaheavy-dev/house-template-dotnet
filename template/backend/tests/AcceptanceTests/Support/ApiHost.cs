using Microsoft.AspNetCore.Mvc.Testing;
using Reqnroll;

namespace AcceptanceTests.Support;

/// <summary>One in-memory API for the whole run; each scenario gets its own HTTP client.</summary>
[Binding]
public sealed class ApiHost
{
    private static WebApplicationFactory<Program>? _factory;

    public static HttpClient CreateClient() =>
        (_factory ?? throw new InvalidOperationException("The API isn't running.")).CreateClient();

    [BeforeTestRun]
    public static void Start() => _factory = new WebApplicationFactory<Program>();

    [AfterTestRun]
    public static async Task Stop()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
    }
}
