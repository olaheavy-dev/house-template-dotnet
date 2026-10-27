using ErrorOr;
using Immediate.Handlers.Shared;
using Microsoft.Extensions.DependencyInjection;
using Modules.Platform.Application.Ping;

namespace IntegrationTests;

/// <summary>Stands in for the ping handler, so a test can make the endpoint fail or throw.</summary>
internal sealed class PingHandlerStub(Func<ErrorOr<PingResponse>> answer) : IHandler<GetPingQuery, ErrorOr<PingResponse>>
{
    public ValueTask<ErrorOr<PingResponse>> HandleAsync(GetPingQuery request, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(answer());

    /// <summary>The API with GET /v1/platform/ping answering whatever <paramref name="answer"/> returns or throws.</summary>
    public static TestApi Api(Func<ErrorOr<PingResponse>> answer) => new(services => services.AddPingHandlerStub(answer));
}

internal static class PingHandlerStubRegistration
{
    /// <summary>Replaces the ping handler with one answering whatever <paramref name="answer"/> returns or throws.</summary>
    public static void AddPingHandlerStub(this IServiceCollection services, Func<ErrorOr<PingResponse>> answer) =>
        services.AddSingleton<IHandler<GetPingQuery, ErrorOr<PingResponse>>>(new PingHandlerStub(answer));
}
