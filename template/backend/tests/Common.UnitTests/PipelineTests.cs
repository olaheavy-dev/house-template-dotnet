using Common.Application.Behaviors;
using ErrorOr;
using FluentValidation;
using Immediate.Handlers.Shared;
using Microsoft.Extensions.DependencyInjection;

[assembly: Behaviors(typeof(LoggingBehavior<,>), typeof(ValidationBehavior<,>))]

namespace Common.UnitTests;

public sealed record EchoQuery(string Text);

internal sealed class EchoQueryValidator : AbstractValidator<EchoQuery>
{
    public EchoQueryValidator() => RuleFor(query => query.Text).NotEmpty().MaximumLength(5);
}

[Handler]
internal static partial class Echo
{
    private static ValueTask<ErrorOr<string>> HandleAsync(EchoQuery query, CancellationToken cancellationToken) =>
        ValueTask.FromResult<ErrorOr<string>>(query.Text);
}

/// <summary>The steps every module's handlers run through, on a handler of the tests' own.</summary>
public sealed class PipelineTests
{
    private static async Task<ErrorOr<string>> Echo(string text)
    {
        await using var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton(TimeProvider.System)
            .AddValidatorsFromAssemblyContaining<PipelineTests>(includeInternalTypes: true)
            .AddCommonUnitTestsHandlers()
            .BuildServiceProvider();
        return await services.GetRequiredService<IHandler<EchoQuery, ErrorOr<string>>>()
            .HandleAsync(new EchoQuery(text), CancellationToken.None);
    }

    [Test]
    public async Task A_valid_request_reaches_the_handler() =>
        Assert.That((await Echo("hi")).Value, Is.EqualTo("hi"));

    [TestCase("")]
    [TestCase("toolong")]
    public async Task An_invalid_request_is_answered_with_validation_errors_and_never_handled(string text)
    {
        var result = await Echo(text);

        Assert.That(result.IsError, Is.True);
        Assert.That(result.Errors.Select(error => (error.Type, error.Code)),
            Is.All.EqualTo((ErrorType.Validation, "Text")));
    }
}
