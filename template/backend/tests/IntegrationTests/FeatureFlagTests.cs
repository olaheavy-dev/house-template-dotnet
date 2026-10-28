using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Common.Application.Flags;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests;

/// <summary>Feature flags from configuration: gates, the public list, and the filters that decide who gets what.</summary>
public sealed class FeatureFlagTests
{
    private static readonly Uri Flags = new("/v1/flags", UriKind.Relative);
    private static readonly Uri NotesList = new("/v1/platform/notes", UriKind.Relative);

    [Test]
    public async Task A_gated_endpoint_answers_404_as_a_problem_while_its_flag_is_off()
    {
        await using var api = new TestApi(_ => { }, settings: Settings(("feature_management:feature_flags:0:enabled", "false")));
        using var client = SignedIn(api, "flags-gate");

        using var response = await client.GetAsync(NotesList);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"));
    }

    [Test]
    public async Task A_gated_endpoint_is_off_when_its_flag_is_not_defined_at_all()
    {
        await using var api = new TestApi(_ => { }, settings: Settings(("feature_management:feature_flags:0:id", "something-else")));
        using var client = SignedIn(api, "flags-undefined");

        using var response = await client.GetAsync(NotesList);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task A_gated_endpoint_works_while_its_flag_is_on_and_other_endpoints_never_notice()
    {
        await using var off = new TestApi(_ => { }, settings: Settings(("feature_management:feature_flags:0:enabled", "false")));
        await using var on = new TestApi(_ => { });
        using var offClient = SignedIn(off, "flags-on");
        using var onClient = SignedIn(on, "flags-on");

        using var enabled = await onClient.GetAsync(NotesList);
        using var otherWhileOff = await offClient.GetAsync(new Uri("/v1/me", UriKind.Relative));

        Assert.That(enabled.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(otherWhileOff.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task The_public_list_shows_only_the_flags_it_allows_and_never_their_rules()
    {
        await using var api = new TestApi(_ => { }, settings: Settings(
            Flag(1, "internal-only", enabled: true),
            Flag(2, "shown", filter: "Microsoft.Percentage", ("Value", "100")),
            ("Flags:Public:1", "shown")));
        using var client = SignedIn(api, "flags-public");

        var body = await Body(client);

        Assert.That(body, Does.Not.Contain("internal-only"), "a flag that is not on the allow-list must not appear");
        Assert.That(body, Does.Not.Contain("Percentage").And.Not.Contain("client_filters").And.Not.Contain("parameters"), "rules must not leak");
        Assert.That(FlagIn(body, "notes-list").GetProperty("enabled").GetBoolean(), Is.True);
        Assert.That(FlagIn(body, "shown").GetProperty("enabled").GetBoolean(), Is.True);
    }

    [Test]
    public async Task A_listed_flag_that_is_not_defined_is_reported_off()
    {
        await using var api = new TestApi(_ => { }, settings: Settings(("Flags:Public:1", "never-defined")));
        using var client = SignedIn(api, "flags-missing");

        var flag = FlagIn(await Body(client), "never-defined");

        Assert.That(flag.GetProperty("enabled").GetBoolean(), Is.False);
    }

    [Test]
    public async Task The_public_list_needs_a_signed_in_user_with_the_permission()
    {
        await using var api = new TestApi(_ => { });
        using var anonymous = api.CreateClient();
        using var withoutPermission = SignedIn(api, "flags-noperm", permissions: "me.read");

        using var unauthenticated = await anonymous.GetAsync(Flags);
        using var forbidden = await withoutPermission.GetAsync(Flags);

        Assert.That(unauthenticated.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(forbidden.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task Percentage_rules_are_all_or_nothing_at_the_extremes()
    {
        await using var api = new TestApi(_ => { }, settings: Settings(
            Flag(1, "all", filter: "Microsoft.Percentage", ("Value", "100")),
            Flag(2, "none", filter: "Microsoft.Percentage", ("Value", "0")),
            ("Flags:Public:1", "all"), ("Flags:Public:2", "none")));
        using var client = SignedIn(api, "flags-percent");

        var body = await Body(client);

        Assert.That(FlagIn(body, "all").GetProperty("enabled").GetBoolean(), Is.True);
        Assert.That(FlagIn(body, "none").GetProperty("enabled").GetBoolean(), Is.False);
    }

    [Test]
    public async Task A_time_window_is_on_only_inside_its_dates()
    {
        var past = DateTimeOffset.UtcNow.AddDays(-2).ToString("o");
        var recent = DateTimeOffset.UtcNow.AddDays(-1).ToString("o");
        var future = DateTimeOffset.UtcNow.AddDays(1).ToString("o");
        await using var api = new TestApi(_ => { }, settings: Settings(
            Flag(1, "open", filter: "Microsoft.TimeWindow", ("Start", past), ("End", future)),
            Flag(2, "over", filter: "Microsoft.TimeWindow", ("Start", past), ("End", recent)),
            Flag(3, "later", filter: "Microsoft.TimeWindow", ("Start", future)),
            ("Flags:Public:1", "open"), ("Flags:Public:2", "over"), ("Flags:Public:3", "later")));
        using var client = SignedIn(api, "flags-window");

        var body = await Body(client);

        Assert.That(FlagIn(body, "open").GetProperty("enabled").GetBoolean(), Is.True);
        Assert.That(FlagIn(body, "over").GetProperty("enabled").GetBoolean(), Is.False);
        Assert.That(FlagIn(body, "later").GetProperty("enabled").GetBoolean(), Is.False);
    }

    [Test]
    public async Task Targeting_aims_a_flag_at_named_users()
    {
        await using var api = new TestApi(_ => { }, settings: Settings(
            Flag(1, "beta", filter: "Microsoft.Targeting", ("Audience:Users:0", "alice"), ("Audience:DefaultRolloutPercentage", "0")),
            ("Flags:Public:1", "beta")));
        using var alice = SignedIn(api, "alice");
        using var bob = SignedIn(api, "bob");

        var forAlice = FlagIn(await Body(alice), "beta").GetProperty("enabled").GetBoolean();
        var forBob = FlagIn(await Body(bob), "beta").GetProperty("enabled").GetBoolean();

        Assert.That(forAlice, Is.True);
        Assert.That(forBob, Is.False);
    }

    [Test]
    public async Task Targeting_aims_a_flag_at_everyone_holding_a_permission()
    {
        await using var api = new TestApi(_ => { }, settings: Settings(
            Flag(1, "writers", filter: "Microsoft.Targeting", ("Audience:Groups:0:Name", "notes.write"), ("Audience:Groups:0:RolloutPercentage", "100"), ("Audience:DefaultRolloutPercentage", "0")),
            ("Flags:Public:1", "writers")));
        using var writer = SignedIn(api, "flags-writer", permissions: "flags.read,notes.write");
        using var reader = SignedIn(api, "flags-reader", permissions: "flags.read");

        Assert.That(FlagIn(await Body(writer), "writers").GetProperty("enabled").GetBoolean(), Is.True);
        Assert.That(FlagIn(await Body(reader), "writers").GetProperty("enabled").GetBoolean(), Is.False);
    }

    [Test]
    public async Task Nobody_signed_in_is_nobody_targeted()
    {
        await using var api = new TestApi(_ => { }, settings: Settings(
            Flag(1, "everyone-at-zero", filter: "Microsoft.Targeting", ("Audience:DefaultRolloutPercentage", "0"))));
        await using var scope = api.Services.CreateAsyncScope();
        var flags = scope.ServiceProvider.GetRequiredService<IFeatureFlags>();

        // No request, so no user: a targeting rule has nobody to match, and a 0% default keeps the flag off.
        Assert.That(await flags.IsEnabledAsync("everyone-at-zero", CancellationToken.None), Is.False);
    }

    [Test]
    public async Task A_variant_flag_gives_each_user_their_assigned_variant()
    {
        await using var api = new TestApi(_ => { }, settings: Settings(
            Flag(1, "model", enabled: true),
            ("feature_management:feature_flags:1:variants:0:name", "large"), ("feature_management:feature_flags:1:variants:0:configuration_value", "big-model"),
            ("feature_management:feature_flags:1:variants:1:name", "small"), ("feature_management:feature_flags:1:variants:1:configuration_value", "small-model"),
            ("feature_management:feature_flags:1:allocation:user:0:variant", "large"), ("feature_management:feature_flags:1:allocation:user:0:users:0", "alice"),
            ("feature_management:feature_flags:1:allocation:default_when_enabled", "small"),
            ("Flags:Public:1", "model")));
        using var alice = SignedIn(api, "alice");
        using var bob = SignedIn(api, "bob");

        var forAlice = FlagIn(await Body(alice), "model");
        var forBob = FlagIn(await Body(bob), "model");

        Assert.That(forAlice.GetProperty("variant").GetString(), Is.EqualTo("large"));
        Assert.That(forBob.GetProperty("variant").GetString(), Is.EqualTo("small"));
    }

    [Test]
    public async Task A_flag_with_telemetry_on_leaves_its_evaluation_on_the_request_trace()
    {
        var events = new List<(string Name, Dictionary<string, string?> Tags)>();
        var listener = new ActivityListener();
        listener.ShouldListenTo = _ => true;
        listener.Sample = (ref _) => ActivitySamplingResult.AllDataAndRecorded;
        listener.ActivityStopped = activity =>
        {
            lock (events)
            {
                events.AddRange(activity.Events.Select(item => (item.Name, item.Tags.ToDictionary(tag => tag.Key, tag => tag.Value?.ToString()))));
            }
        };
        using var registered = listener;
        ActivitySource.AddActivityListener(listener);
        await using var api = new TestApi(_ => { }, settings: Settings(
            Flag(1, "traced", enabled: true),
            ("feature_management:feature_flags:1:telemetry:enabled", "true"),
            ("Flags:Public:1", "traced")));
        using var client = SignedIn(api, "flags-trace");

        await Body(client);

        (string Name, Dictionary<string, string?> Tags)[] seen;
        lock (events)
        {
            seen = [.. events];
        }

        await TestContext.Out.WriteLineAsync("events: " + string.Join(" | ", seen.Select(item => item.Name + "{" + string.Join(",", item.Tags.Select(tag => tag.Key + "=" + tag.Value)) + "}")));
        Assert.That(
            seen.Any(item => item.Tags.ContainsValue("traced")),
            Is.True,
            "the evaluation of a telemetry-enabled flag should be an event on the request's span");
    }

    // A flag in Microsoft's schema at position `index`: on or off, or on through one client filter with its parameters.
    private static (string Key, string Value)[] Flag(int index, string id, bool enabled) =>
    [
        ($"feature_management:feature_flags:{index}:id", id),
        ($"feature_management:feature_flags:{index}:enabled", enabled ? "true" : "false"),
    ];

    private static (string Key, string Value)[] Flag(int index, string id, string filter, params (string Name, string Value)[] parameters) =>
    [
        ($"feature_management:feature_flags:{index}:id", id),
        ($"feature_management:feature_flags:{index}:enabled", "true"),
        ($"feature_management:feature_flags:{index}:conditions:client_filters:0:name", filter),
        .. parameters.Select(parameter => ($"feature_management:feature_flags:{index}:conditions:client_filters:0:parameters:{parameter.Name}", parameter.Value)),
    ];

    // Settings for a test: single (key, value) pairs and the pair lists the Flag helpers return, in one dictionary.
    private static Dictionary<string, string> Settings(params object[] parts)
    {
        var result = new Dictionary<string, string>();
        foreach (var part in parts)
        {
            switch (part)
            {
                case ValueTuple<string, string> pair:
                    result[pair.Item1] = pair.Item2;
                    break;
                case IEnumerable<(string Key, string Value)> pairs:
                    foreach (var (key, value) in pairs)
                    {
                        result[key] = value;
                    }

                    break;
            }
        }

        return result;
    }

    private static HttpClient SignedIn(TestApi api, string user, string? permissions = null)
    {
        var client = api.CreateClient();
        client.DefaultRequestHeaders.Add("X-Dev-User", user);
        if (permissions is not null)
        {
            client.DefaultRequestHeaders.Add("X-Dev-Permissions", permissions);
        }

        return client;
    }

    private static async Task<string> Body(HttpClient client)
    {
        using var response = await client.GetAsync(Flags);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        return await response.Content.ReadAsStringAsync();
    }

    private static JsonElement FlagIn(string body, string key)
    {
        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("flags").EnumerateArray().Single(flag => flag.GetProperty("key").GetString() == key).Clone();
    }
}
