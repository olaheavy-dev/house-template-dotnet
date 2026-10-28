using System.Globalization;
using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using ErrorOr;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using Modules.Platform.Application.Ping;

namespace Modules.Platform.Presentation;

/// <summary>
/// GET /v1/platform/pings: the ping, as Server-Sent Events, now and then every 15 seconds; the
/// example stream every new one copies. Each event has an id, so a client that reconnects with
/// Last-Event-ID carries on from the next one (browsers' EventSource does this itself). Streams
/// opt out of the request timeout.
/// </summary>
internal static class PingStreamEndpoint
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(15);

    internal static void MapPingStream(this IEndpointRouteBuilder app)
    {
        app.MapGet("v1/platform/pings", (
                IHandler<GetPingQuery, ErrorOr<PingResponse>> handler,
                TimeProvider clock,
                IHostApplicationLifetime lifetime,
                [FromHeader(Name = "Last-Event-ID")] string? lastEventId,
                CancellationToken cancellationToken) =>
                TypedResults.ServerSentEvents(Pings(
                    token => handler.HandleAsync(new GetPingQuery(), token), clock, FirstId(lastEventId), cancellationToken, lifetime.ApplicationStopping)))
            .WithName("StreamPings")
            .WithTags("Platform")
            .DisableRequestTimeout();
    }

    private static long FirstId(string? lastEventId) =>
        long.TryParse(lastEventId, NumberStyles.None, CultureInfo.InvariantCulture, out var last) && last < long.MaxValue
            ? last + 1
            : 1;

    private static async IAsyncEnumerable<SseItem<PingResponse>> Pings(
        Func<CancellationToken, ValueTask<ErrorOr<PingResponse>>> ping,
        TimeProvider clock,
        long id,
        CancellationToken requestAborted,
        CancellationToken hostStopping,
        [EnumeratorCancellation] CancellationToken enumerationCancelled = default)
    {
        // The stream ends when the client goes away or when the host starts to stop. Without the second, every
        // connected stream holds a deploy up for the whole shutdown timeout; the client's EventSource reconnects
        // (with Last-Event-ID) to another replica.
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(requestAborted, hostStopping, enumerationCancelled);
        using var timer = new PeriodicTimer(Interval, clock);
        var next = true;
        while (next)
        {
            var result = await ping(stop.Token);
            if (result.IsError)
            {
                yield break;
            }

            yield return new SseItem<PingResponse>(result.Value, "ping") { EventId = id.ToString(CultureInfo.InvariantCulture) };
            id++;
            try
            {
                next = await timer.WaitForNextTickAsync(stop.Token);
            }
            catch (OperationCanceledException)
            {
                next = false;
            }
        }
    }
}
