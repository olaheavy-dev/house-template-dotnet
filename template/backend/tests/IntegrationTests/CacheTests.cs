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
