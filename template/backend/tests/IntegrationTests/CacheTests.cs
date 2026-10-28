using System.Net;
using System.Net.Http.Json;
using Common.Infrastructure.Caching;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;

namespace IntegrationTests;

/// <summary>The cache: per instance by default, shared through Valkey when <c>ConnectionStrings:Cache</c> is set.</summary>
public sealed class CacheTests
{
    // Valkey, a Redis-compatible server under the BSD licence; pinned by digest.
    internal const string ValkeyImage = "valkey/valkey:9.0.7@sha256:7e1deda01b64ece3167d370ab5be3452d29734feec22cbcbc69b3a64f6859d6d";

    [Test]
    public async Task Instances_sharing_valkey_see_the_same_cached_note()
    {
        await using var valkey = await StartValkey();
        var cache = new Dictionary<string, string> { ["ConnectionStrings:Cache"] = $"{valkey.Hostname}:{valkey.GetMappedPublicPort(6379)}" };
        await using var first = new TestApi(_ => { }, settings: cache);
        await using var second = new TestApi(_ => { }, settings: cache);
        using var writer = SignedIn(first, "cache-shared");
        using var reader = SignedIn(second, "cache-shared");
        var id = await CreateNote(writer);
        using var warm = await writer.GetAsync(new Uri($"/v1/platform/notes/{id}", UriKind.Relative));
        await DeleteRow(id);

        // The row is gone, so only the shared cache can still answer the other instance.
        using var fromTheOtherInstance = await reader.GetAsync(new Uri($"/v1/platform/notes/{id}", UriKind.Relative));

        Assert.That(warm.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(fromTheOtherInstance.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task Without_valkey_each_instance_has_its_own_cache()
    {
        await using var first = new TestApi(_ => { });
        await using var second = new TestApi(_ => { });
        using var writer = SignedIn(first, "cache-own");
        using var reader = SignedIn(second, "cache-own");
        var id = await CreateNote(writer);
        using var warm = await writer.GetAsync(new Uri($"/v1/platform/notes/{id}", UriKind.Relative));
        await DeleteRow(id);

        using var stillCached = await writer.GetAsync(new Uri($"/v1/platform/notes/{id}", UriKind.Relative));
        using var notShared = await reader.GetAsync(new Uri($"/v1/platform/notes/{id}", UriKind.Relative));

        Assert.That(warm.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(stillCached.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(notShared.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task A_request_that_gives_up_does_not_fail_the_one_waiting_on_the_same_lookup()
    {
        await using var api = new TestApi(_ => { });
        using var client = SignedIn(api, "cache-cancel");

        // Pairs of reads of the same cold note, the first abandoned straight away: however the lookup is shared,
        // the one that stays must never see an error.
        for (var round = 0; round < 25; round++)
        {
            var id = await CreateNote(client);
            var uri = new Uri($"/v1/platform/notes/{id}", UriKind.Relative);
            using var impatient = new CancellationTokenSource();
            var abandoned = client.GetAsync(uri, impatient.Token);
            var staying = client.GetAsync(uri);
            await impatient.CancelAsync();
            try
            {
                using var ignored = await abandoned;
            }
            catch (OperationCanceledException)
            {
                // The caller that gave up sees its own cancellation.
            }

            using var response = await staying;
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), $"round {round}");
        }
    }

    [Test]
    public async Task A_note_that_was_not_there_is_not_remembered_as_missing()
    {
        await using var valkey = await StartValkey();
        await using var api = new TestApi(_ => { }, settings: SharedCache(valkey));
        using var client = SignedIn(api, "cache-miss");
        var id = await CreateNote(client);
        var other = await CreateNote(client);
        await DeleteRow(id);
        var uri = new Uri($"/v1/platform/notes/{id}", UriKind.Relative);
        using var missing = await client.GetAsync(uri);

        // The same id turns up (a restore, a replica catching up): a remembered miss would hide it for minutes.
        await RestoreRowAs(id, other);
        using var found = await client.GetAsync(uri);

        Assert.That(missing.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(found.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task An_entry_over_the_payload_limit_stays_out_of_valkey()
    {
        await using var valkey = await StartValkey();
        await using var api = new TestApi(_ => { }, settings: SharedCache(valkey, ("Cache:MaximumPayloadBytes", "1024")));
        var cache = api.Services.GetRequiredService<Microsoft.Extensions.Caching.Hybrid.HybridCache>();

        await cache.GetOrCreateAsync("size-small", _ => ValueTask.FromResult("x"));
        await cache.GetOrCreateAsync("size-big", _ => ValueTask.FromResult(new string('x', 4096)));
        await Task.Delay(500);

        var keys = await Valkey(valkey, "--scan");
        Assert.That(keys, Does.Contain("size-small"), "a small entry should reach the shared cache");
        Assert.That(keys, Does.Not.Contain("size-big"), "an entry over MaximumPayloadBytes should not");
    }

    [Test]
    public async Task What_lands_in_valkey_carries_the_prefix_and_an_expiry()
    {
        await using var valkey = await StartValkey();
        await using var api = new TestApi(_ => { }, settings: SharedCache(valkey, ("Cache:KeyPrefix", "probe:"), ("Cache:ExpirationMinutes", "7")));
        using var client = SignedIn(api, "cache-keys");
        var id = await CreateNote(client);
        using var warm = await client.GetAsync(new Uri($"/v1/platform/notes/{id}", UriKind.Relative));

        var keys = (await Valkey(valkey, "--scan")).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var key = keys.Single(candidate => candidate.Contains(id.ToString(), StringComparison.Ordinal));
        var ttl = int.Parse((await Valkey(valkey, "TTL", key)).Trim(), System.Globalization.CultureInfo.InvariantCulture);

        Assert.That(key, Does.StartWith("probe:"));
        Assert.That(ttl, Is.InRange(60, 7 * 60), "the second level should hold the entry for about the configured expiry");
    }

    [Test]
    public async Task Apps_with_different_prefixes_on_one_valkey_do_not_share_entries()
    {
        await using var valkey = await StartValkey();
        await using var one = new TestApi(_ => { }, settings: SharedCache(valkey, ("Cache:KeyPrefix", "one:")));
        await using var two = new TestApi(_ => { }, settings: SharedCache(valkey, ("Cache:KeyPrefix", "two:")));
        using var writer = SignedIn(one, "cache-prefix");
        using var reader = SignedIn(two, "cache-prefix");
        var id = await CreateNote(writer);
        using var warm = await writer.GetAsync(new Uri($"/v1/platform/notes/{id}", UriKind.Relative));
        await DeleteRow(id);

        using var other = await reader.GetAsync(new Uri($"/v1/platform/notes/{id}", UriKind.Relative));

        Assert.That(other.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task A_note_read_through_valkey_comes_back_exactly_as_it_was_stored()
    {
        await using var valkey = await StartValkey();
        var cache = SharedCache(valkey);
        await using var first = new TestApi(_ => { }, settings: cache);
        await using var second = new TestApi(_ => { }, settings: cache);
        using var writer = SignedIn(first, "cache-roundtrip");
        using var reader = SignedIn(second, "cache-roundtrip");
        var id = await CreateNote(writer);
        var uri = new Uri($"/v1/platform/notes/{id}", UriKind.Relative);
        var fromTheDatabase = await (await writer.GetAsync(uri)).Content.ReadAsStringAsync();
        await DeleteRow(id);

        var fromValkey = await (await reader.GetAsync(uri)).Content.ReadAsStringAsync();

        Assert.That(fromValkey, Is.EqualTo(fromTheDatabase));
    }

    [Test]
    public async Task A_removal_on_one_instance_reaches_the_shared_level_but_not_the_other_instances_memory()
    {
        // HybridCache has no backplane: an instance's own memory is only refreshed when its local entry expires.
        await using var valkey = await StartValkey();
        var cache = SharedCache(valkey);
        await using var first = new TestApi(_ => { }, settings: cache);
        await using var second = new TestApi(_ => { }, settings: cache);
        using var one = SignedIn(first, "cache-remove");
        using var two = SignedIn(second, "cache-remove");
        var id = await CreateNote(one);
        var uri = new Uri($"/v1/platform/notes/{id}", UriKind.Relative);
        using var warmFirst = await one.GetAsync(uri);
        using var warmSecond = await two.GetAsync(uri);
        await DeleteRow(id);

        await first.Services.GetRequiredService<Microsoft.Extensions.Caching.Hybrid.HybridCache>().RemoveAsync($"platform:note:{id}");

        using var onTheSecond = await two.GetAsync(uri);
        using var onTheFirst = await one.GetAsync(uri);
        var keysLeft = (await Valkey(valkey, "--scan")).Split('\n', StringSplitOptions.RemoveEmptyEntries).Count(key => key.Contains(id.ToString(), StringComparison.Ordinal));
        Assert.That(keysLeft, Is.Zero, "the removal should have deleted the shared entry, and the miss that followed must not be stored");
        Assert.That(onTheFirst.StatusCode, Is.EqualTo(HttpStatusCode.NotFound), "the instance that removed it should now go to the database");
        await TestContext.Out.WriteLineAsync($"second instance after the first removed the entry: {(int)onTheSecond.StatusCode}");
        Assert.That(onTheSecond.StatusCode, Is.EqualTo(HttpStatusCode.OK), "the other instance still holds its own copy until it expires");
    }

    [Test]
    public async Task When_valkey_goes_down_a_read_still_works_from_the_database()
    {
        var valkey = await StartValkey();
        await using var api = new TestApi(_ => { }, settings: SharedCache(valkey));
        using var client = SignedIn(api, "cache-down");
        var warmed = await CreateNote(client);
        var unseen = await CreateNote(client);
        using var warm = await client.GetAsync(new Uri($"/v1/platform/notes/{warmed}", UriKind.Relative));

        await valkey.StopAsync();
        await valkey.DisposeAsync();
        using var cachedInMemory = await client.GetAsync(new Uri($"/v1/platform/notes/{warmed}", UriKind.Relative));
        using var needsTheDatabase = await client.GetAsync(new Uri($"/v1/platform/notes/{unseen}", UriKind.Relative));

        Assert.That(cachedInMemory.StatusCode, Is.EqualTo(HttpStatusCode.OK), "an entry in the instance's memory should not need Valkey");
        await TestContext.Out.WriteLineAsync($"uncached read with Valkey down: {(int)needsTheDatabase.StatusCode}");
        Assert.That(needsTheDatabase.StatusCode, Is.EqualTo(HttpStatusCode.OK), "a read that missed should fall back to the database, not fail");
    }

    [TestCase("Cache:ExpirationMinutes", "0")]
    [TestCase("Cache:LocalExpirationMinutes", "1441")]
    [TestCase("Cache:MaximumPayloadBytes", "10")]
    [TestCase("Cache:KeyPrefix", "")]
    public void A_setting_out_of_range_stops_the_start(string key, string value)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [key] = value })
            .Build();
        var services = new ServiceCollection();
        services.AddCaching(configuration);
        using var provider = services.BuildServiceProvider();

        var validator = provider.GetRequiredService<IStartupValidator>();

        Assert.Throws<OptionsValidationException>(validator.Validate);
    }

    private static Dictionary<string, string> SharedCache(IContainer valkey, params (string Key, string Value)[] more)
    {
        var settings = new Dictionary<string, string> { ["ConnectionStrings:Cache"] = $"{valkey.Hostname}:{valkey.GetMappedPublicPort(6379)}" };
        foreach (var (key, value) in more)
        {
            settings[key] = value;
        }

        return settings;
    }

    private static async Task<string> Valkey(IContainer valkey, params string[] arguments)
    {
        var result = await valkey.ExecAsync(["valkey-cli", .. arguments]);
        return result.Stdout;
    }

    private static async Task<IContainer> StartValkey()
    {
        var valkey = new ContainerBuilder(ValkeyImage)
            .WithPortBinding(6379, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("Ready to accept connections"))
            .Build();
        await valkey.StartAsync();
        return valkey;
    }

    private static HttpClient SignedIn(TestApi api, string user)
    {
        var client = api.CreateClient();
        client.DefaultRequestHeaders.Add("X-Dev-User", user);
        return client;
    }

    private static async Task<Guid> CreateNote(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync(new Uri("/v1/platform/notes", UriKind.Relative), new { text = "cached" });
        return (await response.Content.ReadFromJsonAsync<NoteBody>())!.Id;
    }

    // Puts a copy of the note `from` back under the id `id`.
    private static async Task RestoreRowAs(Guid id, Guid from)
    {
        await using var connection = new NpgsqlConnection(TestDatabase.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "insert into platform.notes (\"Id\", \"OwnerId\", \"Text\", \"CreatedAt\", \"UpdatedAt\") select @id, \"OwnerId\", \"Text\", \"CreatedAt\", \"UpdatedAt\" from platform.notes where \"Id\" = @from",
            connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("from", from);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task DeleteRow(Guid id)
    {
        await using var connection = new NpgsqlConnection(TestDatabase.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("delete from platform.notes where \"Id\" = @id", connection);
        command.Parameters.AddWithValue("id", id);
        await command.ExecuteNonQueryAsync();
    }

    private sealed record NoteBody(Guid Id);
}
