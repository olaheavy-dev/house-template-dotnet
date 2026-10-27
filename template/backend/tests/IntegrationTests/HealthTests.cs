using System.Net;
using System.Text.Json;
using Common.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace IntegrationTests;

/// <summary>/health/live never runs a check; /health/ready runs the ones tagged ready.</summary>
public sealed class HealthTests
{
    private static readonly Uri Live = new("/health/live", UriKind.Relative);
    private static readonly Uri Ready = new("/health/ready", UriKind.Relative);

    [Test]
    public async Task Ready_is_ok_when_every_readiness_check_passes()
    {
        await using var api = ApiWith(("database", HealthCheckResult.Healthy()));
        using var client = api.CreateClient();

        using var response = await client.GetAsync(Ready);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo("""{"status":"ok"}"""));
    }

    [Test]
    public async Task Ready_is_503_naming_each_failing_check()
    {
        await using var api = ApiWith(
            ("queue", HealthCheckResult.Unhealthy()),
            ("cache", HealthCheckResult.Healthy()),
            ("database", HealthCheckResult.Unhealthy()));
        using var client = api.CreateClient();

        using var response = await client.GetAsync(Ready);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
        Assert.That(body.RootElement.GetProperty("status").GetString(), Is.EqualTo("unavailable"));
        Assert.That(
            body.RootElement.GetProperty("failing").EnumerateArray().Select(name => name.GetString()),
            Is.EqualTo(new[] { "database", "queue" }));
    }

    [Test]
    public async Task Live_is_ok_while_a_dependency_is_down()
    {
        // A slow or failed database must not get the container restarted.
        await using var api = ApiWith(("database", HealthCheckResult.Unhealthy()));
        using var client = api.CreateClient();

        using var response = await client.GetAsync(Live);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo("""{"status":"ok"}"""));
    }

    [Test]
    public async Task Ready_runs_only_checks_tagged_ready()
    {
        await using var api = new TestApi(
            services => services.AddHealthChecks().AddCheck("untagged", () => HealthCheckResult.Unhealthy()));
        using var client = api.CreateClient();

        using var response = await client.GetAsync(Ready);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    private static TestApi ApiWith(params (string Name, HealthCheckResult Result)[] checks) =>
        new(services =>
        {
            var builder = services.AddHealthChecks();
            foreach (var (name, result) in checks)
            {
                builder.AddCheck(name, () => result, tags: [ReadinessChecks.Tag]);
            }
        });
}
