using System.Net;
using System.Text;
using System.Text.Json;
using Common.Application.Idempotency;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace IntegrationTests;

/// <summary>The Idempotency-Key header: a retry with the same key runs the request once and gets the first response back.</summary>
[NonParallelizable]
public sealed class IdempotencyTests
{
    private static readonly Uri Notes = new("/v1/platform/notes", UriKind.Relative);

    [SetUp]
    public void ResetProbe() => IdempotencyProbe.Reset();

    [Test]
    public async Task A_retry_with_the_same_key_creates_one_note_and_gets_the_first_answer_back()
    {
        await using var api = new TestApi(TestEndpoints.AddTo);
        using var client = SignedIn(api, "idem-retry");
        var key = Guid.NewGuid().ToString();

        using var first = await Post(client, Notes, key, """{"text":"buy milk"}""");
        using var second = await Post(client, Notes, key, """{"text":"buy milk"}""");

        Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        Assert.That(second.Headers.Location, Is.EqualTo(first.Headers.Location));
        Assert.That(await second.Content.ReadAsStringAsync(), Is.EqualTo(await first.Content.ReadAsStringAsync()));
        Assert.That(first.Headers.Contains("Idempotency-Replayed"), Is.False);
        Assert.That(second.Headers.GetValues("Idempotency-Replayed"), Is.EqualTo(new[] { "true" }));

        using var list = await client.GetAsync(Notes);
        Assert.That(JsonDocument.Parse(await list.Content.ReadAsStringAsync()).RootElement.GetProperty("items").GetArrayLength(), Is.EqualTo(1));
    }

    [Test]
    public async Task Without_a_key_every_request_runs()
    {
        await using var api = new TestApi(TestEndpoints.AddTo);
        using var client = SignedIn(api, "idem-none");

        using var first = await Post(client, Notes, null, """{"text":"one"}""");
        using var second = await Post(client, Notes, null, """{"text":"one"}""");

        Assert.That(second.Headers.Location, Is.Not.EqualTo(first.Headers.Location));
    }

    [Test]
    public async Task The_same_key_for_a_different_request_is_refused()
    {
        await using var api = new TestApi(TestEndpoints.AddTo);
        using var client = SignedIn(api, "idem-reuse");
        var key = Guid.NewGuid().ToString();
        using var first = await Post(client, Notes, key, """{"text":"buy milk"}""");

        using var second = await Post(client, Notes, key, """{"text":"buy bread"}""");

        Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
        Assert.That(await CodeOf(second), Is.EqualTo("idempotency.key_reused"));
    }

    [Test]
    public async Task Two_users_may_use_the_same_key()
    {
        await using var api = new TestApi(TestEndpoints.AddTo);
        using var alice = SignedIn(api, "idem-alice");
        using var bob = SignedIn(api, "idem-bob");
        var key = Guid.NewGuid().ToString();

        using var first = await Post(alice, Notes, key, """{"text":"same"}""");
        using var second = await Post(bob, Notes, key, """{"text":"same"}""");

        Assert.That(second.Headers.Contains("Idempotency-Replayed"), Is.False);
        Assert.That(second.Headers.Location, Is.Not.EqualTo(first.Headers.Location));
    }

    [Test]
    public async Task An_endpoint_that_requires_a_key_refuses_a_request_without_one()
    {
        await using var api = new TestApi(TestEndpoints.AddTo);
        using var client = api.CreateClient();

        using var response = await Post(client, new Uri("/test/idem-required", UriKind.Relative), null, "{}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await CodeOf(response), Is.EqualTo("idempotency.key_required"));
        Assert.That(IdempotencyProbe.Runs, Is.Zero);
    }

    [TestCase("has a space")]
    [TestCase("tab\there")]
    public async Task A_key_that_is_not_printable_text_is_refused(string key)
    {
        await using var api = new TestApi(TestEndpoints.AddTo);
        using var client = api.CreateClient();
        using var content = new StringContent("{}", Encoding.UTF8, "application/json");
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/test/idem-required", UriKind.Relative));
        request.Content = content;
        request.Headers.TryAddWithoutValidation("Idempotency-Key", key);

        using var response = await client.SendAsync(request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await CodeOf(response), Is.EqualTo("idempotency.key_invalid"));
    }

    [Test]
    public async Task A_key_longer_than_255_characters_is_refused()
    {
        await using var api = new TestApi(TestEndpoints.AddTo);
        using var client = api.CreateClient();

        using var response = await Post(client, new Uri("/test/idem-required", UriKind.Relative), new string('k', 256), "{}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await CodeOf(response), Is.EqualTo("idempotency.key_invalid"));
    }

    [Test]
    public async Task A_retry_while_the_first_request_is_still_running_gets_a_409_and_then_the_first_answer()
    {
        await using var api = new TestApi(TestEndpoints.AddTo);
        using var client = api.CreateClient();
        var slow = new Uri("/test/idem-slow", UriKind.Relative);
        var key = Guid.NewGuid().ToString();
        var running = Post(client, slow, key, "{}");
        await IdempotencyProbe.Started.WaitAsync(TimeSpan.FromSeconds(10));

        using var retry = await Post(client, slow, key, "{}");
        IdempotencyProbe.Gate.Release();
        using var first = await running;
        using var later = await Post(client, slow, key, "{}");

        Assert.That(retry.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That(await CodeOf(retry), Is.EqualTo("idempotency.in_progress"));
        Assert.That(retry.Headers.RetryAfter, Is.Not.Null);
        Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(later.Headers.GetValues("Idempotency-Replayed"), Is.EqualTo(new[] { "true" }));
        Assert.That(IdempotencyProbe.Runs, Is.EqualTo(1));
    }

    [Test]
    public async Task A_server_error_is_not_remembered_so_the_retry_runs_again()
    {
        await using var api = new TestApi(TestEndpoints.AddTo);
        using var client = api.CreateClient();
        var endpoint = new Uri("/test/idem-fails-once", UriKind.Relative);
        var key = Guid.NewGuid().ToString();

        using var first = await Post(client, endpoint, key, "{}");
        using var retry = await Post(client, endpoint, key, "{}");

        Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
        Assert.That(retry.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(retry.Headers.Contains("Idempotency-Replayed"), Is.False);
    }

    [Test]
    public async Task A_request_whose_hold_ran_out_is_taken_over_by_the_retry()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var api = new TestApi(services =>
        {
            TestEndpoints.AddTo(services);
            services.AddSingleton<TimeProvider>(clock);
        });
        using var client = api.CreateClient();
        var slow = new Uri("/test/idem-slow", UriKind.Relative);
        var key = Guid.NewGuid().ToString();
        var abandoned = Post(client, slow, key, "{}");
        await IdempotencyProbe.Started.WaitAsync(TimeSpan.FromSeconds(10));

        // The first attempt is taken to have died once its hold on the key has run out.
        clock.Advance(TimeSpan.FromSeconds(61));
        var takeover = Post(client, slow, key, "{}");
        await IdempotencyProbe.Started.WaitAsync(TimeSpan.FromSeconds(10));
        IdempotencyProbe.Gate.Release(2);
        using var firstResponse = await abandoned;
        using var secondResponse = await takeover;

        Assert.That(IdempotencyProbe.Runs, Is.EqualTo(2));
        Assert.That(firstResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(secondResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task A_key_is_forgotten_once_it_has_expired()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var api = new TestApi(services =>
        {
            TestEndpoints.AddTo(services);
            services.AddSingleton<TimeProvider>(clock);
        });
        using var client = api.CreateClient();
        var endpoint = new Uri("/test/idem-required", UriKind.Relative);
        var key = Guid.NewGuid().ToString();
        using var first = await Post(client, endpoint, key, "{}");

        clock.Advance(TimeSpan.FromHours(25));
        using var again = await Post(client, endpoint, key, "{}");

        Assert.That(again.Headers.Contains("Idempotency-Replayed"), Is.False);
        Assert.That(IdempotencyProbe.Runs, Is.EqualTo(2));
    }

    [Test]
    public async Task The_store_claims_a_key_for_one_caller_only()
    {
        await using var api = new TestApi(_ => { });
        var key = Guid.NewGuid().ToString();

        var attempts = new List<Task<IdempotencyOutcome>>();
        for (var i = 0; i < 8; i++)
        {
            attempts.Add(Begin(api, key));
        }

        var outcomes = await Task.WhenAll(attempts);

        Assert.That(outcomes.Count(outcome => outcome == IdempotencyOutcome.Started), Is.EqualTo(1));
        Assert.That(outcomes.Count(outcome => outcome == IdempotencyOutcome.InProgress), Is.EqualTo(7));
    }

    private static async Task<IdempotencyOutcome> Begin(TestApi api, string key)
    {
        await Task.Yield();
        await using var scope = api.Services.CreateAsyncScope();
        var begin = await scope.ServiceProvider.GetRequiredService<IIdempotencyStore>().BeginAsync("scope", key, "fingerprint", CancellationToken.None);
        return begin.Outcome;
    }

    private static HttpClient SignedIn(TestApi api, string user)
    {
        var client = api.CreateClient();
        client.DefaultRequestHeaders.Add("X-Dev-User", user);
        return client;
    }

    private static async Task<HttpResponseMessage> Post(HttpClient client, Uri uri, string? key, string json)
    {
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var request = new HttpRequestMessage(HttpMethod.Post, uri);
        request.Content = content;
        if (key is not null)
        {
            request.Headers.Add("Idempotency-Key", key);
        }

        return await client.SendAsync(request);
    }

    private static async Task<string?> CodeOf(HttpResponseMessage response)
    {
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return problem.RootElement.GetProperty("code").GetString();
    }
}
