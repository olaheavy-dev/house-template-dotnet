using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IntegrationTests;

/// <summary>X-Request-ID: the caller's when it looks like an id, otherwise new; on every response and log line.</summary>
public sealed class RequestIdTests
{
    internal const string Header = "X-Request-ID";
    private static readonly Uri Ping = new("/v1/platform/ping", UriKind.Relative);

    [Test]
    public async Task A_safe_incoming_id_is_echoed()
    {
        await using var api = new TestApi(_ => { });
        using var client = api.CreateClient();
        client.DefaultRequestHeaders.Add(Header, "req-1.a_B");

        using var response = await client.GetAsync(Ping);

        Assert.That(response.Headers.GetValues(Header), Is.EqualTo(new[] { "req-1.a_B" }));
    }

    [TestCase("has space")]
    [TestCase("tab\tid")]
    [TestCase("{\"forged\":\"log\"}")]
    public async Task An_unsafe_incoming_id_is_replaced_with_a_new_one(string unsafeId)
    {
        await using var api = new TestApi(_ => { });
        using var client = api.CreateClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation(Header, unsafeId);

        using var response = await client.GetAsync(Ping);

        Assert.That(response.Headers.GetValues(Header).Single(), Does.Match("^[0-9a-f]{32}$"));
    }

    [Test]
    public async Task Each_request_without_one_gets_its_own()
    {
        await using var api = new TestApi(_ => { });
        using var client = api.CreateClient();

        using var first = await client.GetAsync(Ping);
        using var second = await client.GetAsync(Ping);

        Assert.That(first.Headers.GetValues(Header).Single(), Is.Not.EqualTo(second.Headers.GetValues(Header).Single()));
    }

    [Test]
    public async Task A_500_problem_carries_the_id()
    {
        await using var api = PingHandlerStub.Api(() => throw new InvalidOperationException("boom"));
        using var client = api.CreateClient();
        client.DefaultRequestHeaders.Add(Header, "req-500");

        using var response = await client.GetAsync(Ping);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
        Assert.That(response.Headers.GetValues(Header), Is.EqualTo(new[] { "req-500" }));
    }

    [Test]
    public async Task The_requests_log_lines_carry_it()
    {
        await using var api = new TestApi(services => services.AddLogging(logging => logging.AddFakeLogging()));
        using var client = api.CreateClient();
        client.DefaultRequestHeaders.Add(Header, "req-logged");

        using var response = await client.GetAsync(Ping);
        var accessLine = api.Services.GetFakeLogCollector().GetSnapshot()
            .Single(record => record.Category == AccessLogTests.Category);

        Assert.That(
            accessLine.Scopes.OfType<IEnumerable<KeyValuePair<string, object>>>().SelectMany(scope => scope),
            Does.Contain(new KeyValuePair<string, object>("RequestId", "req-logged")));
    }
}
