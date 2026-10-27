using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace IntegrationTests;

/// <summary>One access line per request with the route template; never the raw path, query string or a probe.</summary>
public sealed class AccessLogTests
{
    internal const string Category = "Microsoft.AspNetCore.HttpLogging.HttpLoggingMiddleware";

    [Test]
    public async Task A_request_is_one_line_with_its_route_status_and_duration_but_not_its_query()
    {
        var line = (await AccessLinesFor("/v1/platform/ping?email=someone@example.com")).Single();

        Assert.That(line.StructuredState, Does.Contain(new KeyValuePair<string, string?>("Route", "/v1/platform/ping")));
        Assert.That(line.StructuredState, Does.Contain(new KeyValuePair<string, string?>("StatusCode", "200")));
        Assert.That(line.StructuredState!.Select(pair => pair.Key), Does.Contain("Duration"));
        Assert.That(line.Message, Does.Not.Contain("someone@example.com"));
    }

    [Test]
    public async Task A_path_no_route_matches_is_logged_as_unknown()
    {
        var line = (await AccessLinesFor("/v1/users/someone@example.com")).Single();

        Assert.That(line.StructuredState, Does.Contain(new KeyValuePair<string, string?>("Route", "Unknown")));
        Assert.That(line.Message, Does.Not.Contain("someone@example.com"));
    }

    [Test]
    public async Task No_log_line_carries_the_raw_path_in_a_scope()
    {
        // ASP.NET Core's hosting diagnostics would add RequestPath to every line's scopes.
        await using var api = new TestApi(services => services.AddLogging(logging => logging.AddFakeLogging()));
        using var client = api.CreateClient();

        using var response = await client.GetAsync(new Uri("/v1/users/someone@example.com", UriKind.Relative));
        var records = api.Services.GetFakeLogCollector().GetSnapshot();

        Assert.That(records, Is.Not.Empty);
        Assert.That(
            records.SelectMany(record => record.Scopes).Select(scope => scope?.ToString()),
            Has.None.Contains("someone@example.com"));
        Assert.That(records.Select(record => record.Message), Has.None.Contains("someone@example.com"));
    }

    [Test]
    public async Task Health_probes_are_not_logged()
    {
        Assert.That(await AccessLinesFor("/health/live"), Is.Empty);
    }

    private static async Task<IReadOnlyList<FakeLogRecord>> AccessLinesFor(string path)
    {
        await using var api = new TestApi(services => services.AddLogging(logging => logging.AddFakeLogging()));
        using var client = api.CreateClient();

        using var response = await client.GetAsync(new Uri(path, UriKind.Relative));

        return [.. api.Services.GetFakeLogCollector().GetSnapshot().Where(record => record.Category == Category)];
    }
}
