using Common.Application.Messaging;
using ErrorOr;

namespace Modules.Platform.Application.Ping;

/// <summary>Whether the API is up, and what it is called. The example query every new module copies.</summary>
public sealed record GetPingQuery : IQuery<PingResponse>;

public sealed record PingResponse(string Status, string Service, DateTimeOffset At);

internal sealed class GetPingQueryHandler(TimeProvider clock, ServiceName service)
    : IQueryHandler<GetPingQuery, PingResponse>
{
    public Task<ErrorOr<PingResponse>> Handle(GetPingQuery query, CancellationToken cancellationToken) =>
        Task.FromResult<ErrorOr<PingResponse>>(new PingResponse("ok", service.Value, clock.GetUtcNow()));
}
