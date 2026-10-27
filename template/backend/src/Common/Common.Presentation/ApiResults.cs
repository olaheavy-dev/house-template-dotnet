using ErrorOr;
using Microsoft.AspNetCore.Http;

namespace Common.Presentation;

/// <summary>
/// Turns expected failures into RFC 9457 problem responses. The first error sets the status; its
/// stable code (<c>area.reason</c>) is the <c>code</c> extension, so clients branch on the code rather
/// than the wording. Validation errors become one validation problem listing every field.
/// </summary>
public static class ApiResults
{
    public static IResult Problem(IReadOnlyList<Error> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (errors.Count == 0)
        {
            throw new ArgumentException("A problem needs at least one error.", nameof(errors));
        }

        if (errors.All(error => error.Type == ErrorType.Validation))
        {
            return Results.ValidationProblem(
                errors.GroupBy(error => error.Code).ToDictionary(
                    group => group.Key,
                    group => group.Select(error => error.Description).ToArray()),
                extensions: new Dictionary<string, object?> { ["code"] = "request.invalid" });
        }

        var first = errors[0];
        return Results.Problem(
            statusCode: StatusFor(first.Type),
            detail: first.Description,
            extensions: new Dictionary<string, object?> { ["code"] = first.Code });
    }

    /// <summary>The value as 200 OK, or the errors as a problem.</summary>
    public static IResult Ok<TValue>(ErrorOr<TValue> result) =>
        result.Match(value => Results.Ok(value), Problem);

    /// <summary>204 No Content, or the errors as a problem.</summary>
    public static IResult NoContent(ErrorOr<Success> result) =>
        result.Match(_ => Results.NoContent(), Problem);

    private static int StatusFor(ErrorType type) => type switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorType.Forbidden => StatusCodes.Status403Forbidden,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status500InternalServerError,
    };
}
