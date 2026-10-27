using ErrorOr;
using FluentValidation;
using Immediate.Handlers.Shared;

namespace Common.Application.Behaviors;

/// <summary>
/// Runs every FluentValidation validator for the request; any failure becomes validation errors
/// instead of calling the handler. Applies only to handlers that answer ErrorOr.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : Behavior<TRequest, TResponse>
    where TResponse : IErrorOr
{
    public override async ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken)
    {
        var failures = new List<Error>();
        foreach (var validator in validators)
        {
            var result = await validator.ValidateAsync(request, cancellationToken);
            failures.AddRange(result.Errors.Select(f => Error.Validation(f.PropertyName, f.ErrorMessage)));
        }

        return failures.Count == 0 ? await Next(request, cancellationToken) : ErrorResponse.From<TResponse>(failures);
    }
}
