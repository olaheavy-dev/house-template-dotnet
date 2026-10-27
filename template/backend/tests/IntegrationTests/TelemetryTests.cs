using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
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

    [Test]
    public async Task The_service_is_named_after_the_app_when_nothing_names_it()
    {
        // Codex review on PR 7: the SDK alone names it unknown_service:dotnet, like every other app.
        // A generated project's appsettings.json names it; blank that out.
        var settings = new Dictionary<string, string> { ["OpenTelemetry:ServiceName"] = "" };

        Assert.That(await ServiceNameWith(settings), Is.EqualTo("Api"));
    }

    [Test]
    public async Task The_settings_name_the_service()
    {
        var settings = new Dictionary<string, string> { ["OpenTelemetry:ServiceName"] = "demo-app-api" };

        Assert.That(await ServiceNameWith(settings), Is.EqualTo("demo-app-api"));
    }

    [Test]
    public async Task OTEL_SERVICE_NAME_wins_over_the_settings()
    {
        var settings = new Dictionary<string, string>
        {
            ["OpenTelemetry:ServiceName"] = "demo-app-api",
            ["OTEL_SERVICE_NAME"] = "demo-app-api-eu",
        };

        Assert.That(await ServiceNameWith(settings), Is.EqualTo("demo-app-api-eu"));
    }

    private static async Task<object?> ServiceNameWith(IReadOnlyDictionary<string, string> settings)
    {
        await using var api = new TestApi(_ => { }, settings: settings);
        using var client = api.CreateClient();
        var resource = api.Services.GetRequiredService<TracerProvider>().GetResource();
        return resource.Attributes.Single(attribute => attribute.Key == "service.name").Value;
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
