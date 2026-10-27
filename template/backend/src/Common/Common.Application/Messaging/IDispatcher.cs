using ErrorOr;

namespace Common.Application.Messaging;

/// <summary>
/// Sends a command or query to its one handler. Endpoints depend on this, not on handlers, so
/// cross-cutting steps (validation, logging, transactions) can wrap every handler in one place.
/// </summary>
public interface IDispatcher
{
    Task<ErrorOr<Success>> Send(ICommand command, CancellationToken cancellationToken);

    Task<ErrorOr<TResponse>> Send<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken);

    Task<ErrorOr<TResponse>> Query<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken);
}
