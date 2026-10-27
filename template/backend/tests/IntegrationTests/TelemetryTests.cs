using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;

namespace IntegrationTests;

/// <summary>Requests are traced; health probes aren't.</summary>
public sealed class TelemetryTests
{
    // A request span ends just after the client has its response, so the tests wait for it.
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);

    [Test]
    public async Task A_request_is_traced_with_its_route()
    {
        var spans = new List<Activity>();
        await using var api = TracedApi(spans);
        using var client = api.CreateClient();

        using var response = await client.GetAsync(new Uri("/v1/platform/ping", UriKind.Relative));

        Assert.That(await Eventually(() => spans.ToArray().Any(span => span.DisplayName == "GET v1/platform/ping")), Is.True);
    }

    [Test]
    public async Task A_health_probe_is_not_traced()
    {
        var spans = new List<Activity>();
        await using var api = TracedApi(spans);
        using var client = api.CreateClient();

        using var probe = await client.GetAsync(new Uri("/health/live", UriKind.Relative));
        // A request after the probe: once its span is in, the probe's would be too.
        using var marker = await client.GetAsync(new Uri("/marker", UriKind.Relative));

        Assert.That(await Eventually(() => spans.ToArray().Length > 0), Is.True);
        Assert.That(spans.Select(span => span.GetTagItem("url.path")), Is.EqualTo(new[] { "/marker" }));
    }

    private static TestApi TracedApi(List<Activity> spans) =>
        new(services => services.AddOpenTelemetry().WithTracing(tracing => tracing.AddInMemoryExporter(spans)));

    private static async Task<bool> Eventually(Func<bool> condition)
    {
        var deadline = DateTimeOffset.UtcNow + Patience;
        while (!condition() && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(20);
        }

        return condition();
    }
}
