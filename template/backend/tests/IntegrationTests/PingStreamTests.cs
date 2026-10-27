using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace IntegrationTests;

/// <summary>GET /v1/platform/pings: Server-Sent Events with ids, resumable, and exempt from the request timeout.</summary>
public sealed class PingStreamTests
{
    private static readonly Uri Pings = new("/v1/platform/pings", UriKind.Relative);

    [Test]
    public async Task The_first_event_is_a_ping_with_id_1()
    {
        await using var api = new TestApi(services => services.AddSingleton<TimeProvider>(new FakeTimeProvider()));
        using var stream = await Open(api, lastEventId: null);

        var ping = await stream.NextEvent();

        Assert.That(stream.Response.Content.Headers.ContentType?.MediaType, Is.EqualTo("text/event-stream"));
        Assert.That(ping["event"], Is.EqualTo("ping"));
        Assert.That(ping["id"], Is.EqualTo("1"));
        Assert.That(ping["data"], Does.Contain("\"status\":\"ok\""));
    }

    [Test]
    public async Task A_reconnecting_client_carries_on_after_its_last_event_id()
    {
        await using var api = new TestApi(services => services.AddSingleton<TimeProvider>(new FakeTimeProvider()));
        using var stream = await Open(api, lastEventId: "41");

        Assert.That((await stream.NextEvent())["id"], Is.EqualTo("42"));
    }

    [Test]
    public async Task The_stream_outlives_the_request_timeout()
    {
        var clock = new FakeTimeProvider();
        await using var api = new TestApi(
            services => services.AddSingleton<TimeProvider>(clock),
            settings: new Dictionary<string, string> { ["Api:RequestTimeoutSeconds"] = "1" });
        using var stream = await Open(api, lastEventId: null);
        await stream.NextEvent();

        await Task.Delay(TimeSpan.FromSeconds(1.5));
        clock.Advance(TimeSpan.FromSeconds(15));

        Assert.That((await stream.NextEvent())["id"], Is.EqualTo("2"));
    }

    private static async Task<EventStream> Open(TestApi api, string? lastEventId)
    {
        var client = api.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, Pings);
        if (lastEventId is not null)
        {
            request.Headers.Add("Last-Event-ID", lastEventId);
        }

        var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        return new EventStream(client, response);
    }

    private sealed class EventStream(HttpClient client, HttpResponseMessage response) : IDisposable
    {
        private readonly StreamReader _reader = new(response.Content.ReadAsStream());

        public HttpResponseMessage Response => response;

        /// <summary>The next event's fields (event, id, data), read up to its blank line.</summary>
        public async Task<Dictionary<string, string>> NextEvent()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var fields = new Dictionary<string, string>();
            while (await _reader.ReadLineAsync(timeout.Token) is { } line)
            {
                if (line.Length == 0 && fields.Count > 0)
                {
                    return fields;
                }

                var colon = line.IndexOf(':', StringComparison.Ordinal);
                if (colon > 0)
                {
                    fields[line[..colon]] = line[(colon + 1)..].TrimStart();
                }
            }

            throw new EndOfStreamException("The stream ended.");
        }

        public void Dispose()
        {
            _reader.Dispose();
            response.Dispose();
            client.Dispose();
        }
    }
}
