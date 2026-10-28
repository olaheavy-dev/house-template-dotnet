using Common.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace IntegrationTests;

/// <summary>
/// One Postgres for the whole run, in a container (needs Docker), with every module's migrations
/// applied, so every <see cref="TestApi"/> starts against the real schema.
/// </summary>
[SetUpFixture]
public sealed class TestDatabase
{
    // The image backend/compose.yaml will run, pinned by digest.
    internal const string Image = "postgres:17.11@sha256:67f41722b7a8cbdb868a44a4995c846eddfdc2973bccb291ce937dce88ad5675";

    private PostgreSqlContainer? _postgres;

    internal static string ConnectionString { get; private set; } = string.Empty;

    [OneTimeSetUp]
    public async Task StartPostgres()
    {
        _postgres = new PostgreSqlBuilder(Image).Build();
        await _postgres.StartAsync();
        ConnectionString = _postgres.GetConnectionString();

        await using var host = new MigratingApi(ConnectionString);
        await host.Services.MigrateDatabaseAsync();
    }

    private sealed class MigratingApi(string connectionString) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) =>
            builder.UseSetting("ConnectionStrings:Default", connectionString);
    }

    [OneTimeTearDown]
    public async Task StopPostgres()
    {
        if (_postgres is not null)
        {
            await _postgres.DisposeAsync();
        }
    }
}
