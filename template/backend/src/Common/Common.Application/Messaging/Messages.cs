using ErrorOr;

namespace Common.Application.Messaging;

/// <summary>A request that changes state and returns only whether it worked.</summary>
public interface ICommand;

/// <summary>A request that changes state and returns a value.</summary>
public interface ICommand<TResponse>;

/// <summary>A request that reads state and changes nothing.</summary>
public interface IQuery<TResponse>;

public interface ICommandHandler<in TCommand>
    where TCommand : ICommand
{
    Task<ErrorOr<Success>> Handle(TCommand command, CancellationToken cancellationToken);
}

public interface ICommandHandler<in TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    Task<ErrorOr<TResponse>> Handle(TCommand command, CancellationToken cancellationToken);
}

public interface IQueryHandler<in TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
    Task<ErrorOr<TResponse>> Handle(TQuery query, CancellationToken cancellationToken);
}
