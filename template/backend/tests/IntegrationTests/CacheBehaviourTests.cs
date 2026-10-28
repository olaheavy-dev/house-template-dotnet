using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using Common.Infrastructure.Caching;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace IntegrationTests;

/// <summary>How HybridCache behaves under the settings this template gives it, probed directly.</summary>
public sealed class CacheBehaviourTests
{
    [Test]
    public async Task A_factory_that_fails_is_not_remembered()
    {
        await using var provider = Cache();
        var cache = provider.GetRequiredService<HybridCache>();
        var calls = 0;

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await cache.GetOrCreateAsync<string>("failing", _ =>
            {
                calls++;
                throw new InvalidOperationException("store is down");
            }));
        var second = await cache.GetOrCreateAsync("failing", _ =>
        {
            calls++;
            return ValueTask.FromResult("recovered");
        });

        Assert.That(second, Is.EqualTo("recovered"));
        Assert.That(calls, Is.EqualTo(2));
    }

    [Test]
    public async Task A_caller_that_gives_up_does_not_take_the_others_down_with_it()
    {
        await using var provider = Cache();
        var cache = provider.GetRequiredService<HybridCache>();
        var release = new TaskCompletionSource();
        var started = new TaskCompletionSource();
        using var impatient = new CancellationTokenSource();

        var first = cache.GetOrCreateAsync("shared", async _ =>
        {
            started.TrySetResult();
            await release.Task;
            return "value";
        }, cancellationToken: impatient.Token).AsTask();
        await started.Task;
        var patient = cache.GetOrCreateAsync("shared", _ => ValueTask.FromResult("not used"), cancellationToken: CancellationToken.None).AsTask();
        await impatient.CancelAsync();
        release.SetResult();

        Assert.That(await patient, Is.EqualTo("value"));
        try
        {
            await first;
        }
        catch (OperationCanceledException)
        {
            // The caller that cancelled may see its own cancellation; the point is the other one does not.
        }
    }

    [Test]
    public async Task The_configured_expiries_and_limits_reach_the_cache()
    {
        // HybridCache keeps its own clock, so expiry cannot be driven with a fake one and the real minimum is a minute:
        // check the settings arrive, and the Valkey tests check the shared level really expires.
        await using var provider = Cache(("Cache:ExpirationMinutes", "7"), ("Cache:LocalExpirationMinutes", "3"), ("Cache:MaximumPayloadBytes", "2048"));

        var options = provider.GetRequiredService<IOptions<HybridCacheOptions>>().Value;

        Assert.That(options.DefaultEntryOptions!.Expiration, Is.EqualTo(TimeSpan.FromMinutes(7)));
        Assert.That(options.DefaultEntryOptions.LocalCacheExpiration, Is.EqualTo(TimeSpan.FromMinutes(3)));
        Assert.That(options.MaximumPayloadBytes, Is.EqualTo(2048));
    }

    [Test]
    public async Task An_unreachable_valkey_costs_a_read_milliseconds_not_seconds()
    {
        await using var api = new TestApi(_ => { }, settings: new Dictionary<string, string> { ["ConnectionStrings:Cache"] = "127.0.0.1:1" });
        using var client = api.CreateClient();
        client.DefaultRequestHeaders.Add("X-Dev-User", "cache-unreachable");
        using var created = await client.PostAsJsonAsync(new Uri("/v1/platform/notes", UriKind.Relative), new { text = "no valkey" });
        var id = (await created.Content.ReadFromJsonAsync<NoteBody>())!.Id;

        var clock = Stopwatch.StartNew();
        using var read = await client.GetAsync(new Uri($"/v1/platform/notes/{id}", UriKind.Relative));
        clock.Stop();
        var next = await CreateNoteId(client);
        var later = Stopwatch.StartNew();
        using var readAgain = await client.GetAsync(new Uri($"/v1/platform/notes/{next}", UriKind.Relative));
        later.Stop();
        Assert.That(clock.Elapsed, Is.LessThan(TimeSpan.FromMilliseconds(750)), $"first read took {clock.ElapsedMilliseconds} ms");
        Assert.That(later.Elapsed, Is.LessThan(TimeSpan.FromMilliseconds(750)), $"second read took {later.ElapsedMilliseconds} ms");
        Assert.That(readAgain.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        Assert.That(read.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    private static async Task<Guid> CreateNoteId(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync(new Uri("/v1/platform/notes", UriKind.Relative), new { text = "another" });
        return (await response.Content.ReadFromJsonAsync<NoteBody>())!.Id;
    }

    private static ServiceProvider Cache(params (string Key, string Value)[] settings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings.ToDictionary(setting => setting.Key, setting => (string?)setting.Value))
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCaching(configuration);
        return services.BuildServiceProvider();
    }

    private sealed record NoteBody(Guid Id);
}
