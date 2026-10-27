using Immediate.Handlers.Shared;
using Microsoft.Extensions.Logging;

namespace Common.Application.Behaviors;

/// <summary>Logs each request's name and how long it took.</summary>
public sealed partial class LoggingBehavior<TRequest, TResponse>(
    ILogger<LoggingBehavior<TRequest, TResponse>> logger,
    TimeProvider clock)
    : Behavior<TRequest, TResponse>
{
    private static readonly string RequestName = typeof(TRequest).Name;

    public override async ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken)
    {
        var started = clock.GetTimestamp();
        var response = await Next(request, cancellationToken);
        var elapsed = clock.GetElapsedTime(started);
        Handled(logger, RequestName, elapsed);
        return response;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Handled {Request} in {Elapsed}")]
    private static partial void Handled(ILogger logger, string request, TimeSpan elapsed);
}
