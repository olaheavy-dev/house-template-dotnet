using Common.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Reqnroll;
using Testcontainers.PostgreSql;

namespace AcceptanceTests.Support;

/// <summary>One in-memory API on a real Postgres (a container; needs Docker) for the whole run; each scenario gets its own HTTP client.</summary>
[Binding]
public sealed class ApiHost
{
    // The image backend/compose.yaml will run, pinned by digest.
    internal const string Image = "postgres:17.11@sha256:67f41722b7a8cbdb868a44a4995c846eddfdc2973bccb291ce937dce88ad5675";

    private static PostgreSqlContainer? _postgres;
    private static WebApplicationFactory<Program>? _factory;

    public static HttpClient CreateClient() =>
        (_factory ?? throw new InvalidOperationException("The API isn't running.")).CreateClient();

    [BeforeTestRun]
    public static async Task Start()
    {
        _postgres = new PostgreSqlBuilder(Image).Build();
        await _postgres.StartAsync();
        var connectionString = _postgres.GetConnectionString();

        _factory = new ApiFactory(connectionString);
        await _factory.Services.MigrateModuleDatabasesAsync();
    }

    private sealed class ApiFactory(string connectionString) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder) =>
            builder.UseSetting("ConnectionStrings:Default", connectionString);
    }

    [AfterTestRun]
    public static async Task Stop()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        if (_postgres is not null)
        {
            await _postgres.DisposeAsync();
        }
    }
}
