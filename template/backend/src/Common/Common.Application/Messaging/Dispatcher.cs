using System.Collections.Concurrent;
using ErrorOr;
using Microsoft.Extensions.DependencyInjection;

namespace Common.Application.Messaging;

/// <summary>
/// Resolves the handler for a request's runtime type from the request's scope. Handlers are usually
/// internal to their module, so the call goes through a small typed wrapper per request type (built
/// once, then cached) instead of reflection or <c>dynamic</c> on every call.
/// </summary>
internal sealed class Dispatcher(IServiceProvider services) : IDispatcher
{
    private static readonly ConcurrentDictionary<Type, object> Wrappers = new();

    public Task<ErrorOr<Success>> Send(ICommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var wrapper = (VoidCommandWrapper)Wrappers.GetOrAdd(
            command.GetType(),
            type => Activator.CreateInstance(typeof(VoidCommandWrapper<>).MakeGenericType(type))!);
        return wrapper.Handle(command, services, cancellationToken);
    }

    public Task<ErrorOr<TResponse>> Send<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var wrapper = (ResponseCommandWrapper<TResponse>)Wrappers.GetOrAdd(
            command.GetType(),
            type => Activator.CreateInstance(typeof(ResponseCommandWrapper<,>).MakeGenericType(type, typeof(TResponse)))!);
        return wrapper.Handle(command, services, cancellationToken);
    }

    public Task<ErrorOr<TResponse>> Query<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var wrapper = (QueryWrapper<TResponse>)Wrappers.GetOrAdd(
            query.GetType(),
            type => Activator.CreateInstance(typeof(QueryWrapper<,>).MakeGenericType(type, typeof(TResponse)))!);
        return wrapper.Handle(query, services, cancellationToken);
    }

    private abstract class VoidCommandWrapper
    {
        public abstract Task<ErrorOr<Success>> Handle(ICommand command, IServiceProvider services, CancellationToken cancellationToken);
    }

    private sealed class VoidCommandWrapper<TCommand> : VoidCommandWrapper
        where TCommand : ICommand
    {
        public override Task<ErrorOr<Success>> Handle(ICommand command, IServiceProvider services, CancellationToken cancellationToken) =>
            services.GetRequiredService<ICommandHandler<TCommand>>().Handle((TCommand)command, cancellationToken);
    }

    private abstract class ResponseCommandWrapper<TResponse>
    {
        public abstract Task<ErrorOr<TResponse>> Handle(
            ICommand<TResponse> command, IServiceProvider services, CancellationToken cancellationToken);
    }

    private sealed class ResponseCommandWrapper<TCommand, TResponse> : ResponseCommandWrapper<TResponse>
        where TCommand : ICommand<TResponse>
    {
        public override Task<ErrorOr<TResponse>> Handle(
            ICommand<TResponse> command, IServiceProvider services, CancellationToken cancellationToken) =>
            services.GetRequiredService<ICommandHandler<TCommand, TResponse>>().Handle((TCommand)command, cancellationToken);
    }

    private abstract class QueryWrapper<TResponse>
    {
        public abstract Task<ErrorOr<TResponse>> Handle(
            IQuery<TResponse> query, IServiceProvider services, CancellationToken cancellationToken);
    }

    private sealed class QueryWrapper<TQuery, TResponse> : QueryWrapper<TResponse>
        where TQuery : IQuery<TResponse>
    {
        public override Task<ErrorOr<TResponse>> Handle(
            IQuery<TResponse> query, IServiceProvider services, CancellationToken cancellationToken) =>
            services.GetRequiredService<IQueryHandler<TQuery, TResponse>>().Handle((TQuery)query, cancellationToken);
    }
}
