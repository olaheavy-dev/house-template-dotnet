using System.Text.Json;
using Api;
using Npgsql;

namespace IntegrationTests;

/// <summary>What the container image leans on: the commit on /health/live, the healthcheck command, and the migrate step.</summary>
public sealed class ImageSupportTests
{
    [Test]
    public async Task Live_reports_the_commit_the_image_was_built_from()
    {
        await using var api = new TestApi(_ => { }, settings: new Dictionary<string, string> { ["GIT_COMMIT"] = "9fffcea" });
        using var client = api.CreateClient();

        using var response = await client.GetAsync(new Uri("/health/live", UriKind.Relative));
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.That(body.RootElement.GetProperty("status").GetString(), Is.EqualTo("ok"));
        Assert.That(body.RootElement.GetProperty("commit").GetString(), Is.EqualTo("9fffcea"));
    }

    [Test]
    public async Task Live_says_nothing_about_a_commit_when_there_is_none()
    {
        await using var api = new TestApi(_ => { });
        using var client = api.CreateClient();

        using var response = await client.GetAsync(new Uri("/health/live", UriKind.Relative));

        Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo("""{"status":"ok"}"""));
    }

    [Test]
    public async Task The_healthcheck_command_succeeds_against_a_running_api_and_fails_against_nothing()
    {
        await using var api = new TestApi(_ => { });
        api.UseKestrel(0);
        api.StartServer();
        var address = api.ClientOptions.BaseAddress.ToString();

        var running = await HealthProbe.RunAsync(address);
        var nothing = await HealthProbe.RunAsync("http://127.0.0.1:1");

        Assert.That(running, Is.Zero);
        Assert.That(nothing, Is.EqualTo(1));
    }

    [TestCase(null, "http://localhost:8080/")]
    [TestCase("", "http://localhost:8080/")]
    [TestCase("http://+:8080", "http://localhost:8080/")]
    [TestCase("http://*:9000", "http://localhost:9000/")]
    [TestCase("http://0.0.0.0:8081;https://+:8443", "http://localhost:8081/")]
    [TestCase("http://api.internal:8080", "http://api.internal:8080/")]
    public void The_healthcheck_asks_the_address_the_api_listens_on(string? urls, string expected) =>
        Assert.That(HealthProbe.AddressOf(urls).ToString(), Is.EqualTo(expected));

    [Test]
    public async Task The_migrate_step_creates_the_messaging_tables_as_well_as_the_modules()
    {
        await using var connection = new NpgsqlConnection(TestDatabase.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "select table_name from information_schema.tables where table_schema = 'wolverine' order by 1", connection);
        await using var reader = await command.ExecuteReaderAsync();
        var tables = new List<string>();
        while (await reader.ReadAsync())
        {
            tables.Add(reader.GetString(0));
        }

        Assert.That(tables, Does.Contain("wolverine_incoming_envelopes").And.Contain("wolverine_outgoing_envelopes").And.Contain("wolverine_dead_letters"));
    }

    [Test]
    public async Task A_production_api_does_not_create_the_messaging_tables_itself_it_refuses_to_start_without_them()
    {
        await using var postgres = new Testcontainers.PostgreSql.PostgreSqlBuilder(TestDatabase.Image).Build();
        await postgres.StartAsync();
        await using var api = new TestApi(
            _ => { },
            environment: "Production",
            settings: new Dictionary<string, string> { ["ConnectionStrings:Default"] = postgres.GetConnectionString() });

        // No migrate step has run: the schema is the deployer's to change, not the API's.
        string? failure = null;
        try
        {
            using var client = api.CreateClient();
        }
        catch (AggregateException exception)
        {
            failure = exception.ToString();
        }

        Assert.That(failure, Is.Not.Null, "the API started without its messaging tables");
        Assert.That(failure, Does.Contain("wolverine"));
    }
}
