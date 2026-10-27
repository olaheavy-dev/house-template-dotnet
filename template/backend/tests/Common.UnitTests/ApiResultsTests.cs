using Common.Presentation;
using ErrorOr;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Common.UnitTests;

public sealed class ApiResultsTests
{
    [TestCase(ErrorType.NotFound, 404)]
    [TestCase(ErrorType.Conflict, 409)]
    [TestCase(ErrorType.Forbidden, 403)]
    [TestCase(ErrorType.Unauthorized, 401)]
    [TestCase(ErrorType.Failure, 500)]
    [TestCase(ErrorType.Unexpected, 500)]
    public void Problem_sets_the_status_from_the_first_errors_type_and_carries_its_code(ErrorType type, int status)
    {
        var error = Error.Custom((int)type, "thing.reason", "What went wrong.");

        var problem = (ProblemHttpResult)ApiResults.Problem([error, Error.Failure("other.reason")]);

        Assert.That(problem.StatusCode, Is.EqualTo(status));
        Assert.That(problem.ProblemDetails.Extensions["code"], Is.EqualTo("thing.reason"));
        Assert.That(problem.ProblemDetails.Detail, Is.EqualTo("What went wrong."));
    }

    [Test]
    public void Problem_lists_every_validation_error_by_field()
    {
        var problem = (ProblemHttpResult)ApiResults.Problem(
        [
            Error.Validation("title", "Title is required."),
            Error.Validation("title", "Title is too long."),
            Error.Validation("date", "Date is in the past."),
        ]);
        var details = (HttpValidationProblemDetails)problem.ProblemDetails;

        Assert.That(problem.StatusCode, Is.EqualTo(400));
        Assert.That(details.Errors["title"], Is.EqualTo(new[] { "Title is required.", "Title is too long." }));
        Assert.That(details.Errors["date"], Is.EqualTo(new[] { "Date is in the past." }));
        Assert.That(details.Extensions["code"], Is.EqualTo("request.invalid"));
    }

    [Test]
    public void Problem_without_errors_is_a_bug() =>
        Assert.That(() => ApiResults.Problem([]), Throws.ArgumentException);

    [Test]
    public void Ok_answers_the_value_or_the_problem()
    {
        var ok = (Ok<int>)ApiResults.Ok<int>(42);
        var missing = (ProblemHttpResult)ApiResults.Ok<int>(Error.NotFound("thing.missing"));

        Assert.That(ok.Value, Is.EqualTo(42));
        Assert.That(missing.StatusCode, Is.EqualTo(404));
    }
}
