using ErrorOr;
using Immediate.Handlers.Shared;

namespace Modules.Platform.Application.Ping;

/// <summary>Whether the API is up, and what it is called. The example query every new module copies.</summary>
public sealed record GetPingQuery;

public sealed record PingResponse(string Status, string Service, DateTimeOffset At);

[Handler]
internal sealed partial class GetPing(TimeProvider clock, ServiceName service)
{
    private ValueTask<ErrorOr<PingResponse>> HandleAsync(GetPingQuery query, CancellationToken cancellationToken) =>
        ValueTask.FromResult<ErrorOr<PingResponse>>(new PingResponse("ok", service.Value, clock.GetUtcNow()));
}
