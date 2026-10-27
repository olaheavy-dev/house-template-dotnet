using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Common.Presentation;

public static class ProblemDetailsSetup
{
    /// <summary>The code of the 500 problem an unhandled exception becomes.</summary>
    public const string UnexpectedCode = "internal.unexpected";

    private static readonly Dictionary<int, (string Code, string Detail)> SignInProblems = new()
    {
        [StatusCodes.Status401Unauthorized] = ("auth.unauthenticated", "Sign in to use this."),
        [StatusCodes.Status403Forbidden] = ("auth.forbidden", "You don't have permission to do this."),
    };

    /// <summary>
    /// Every error the API answers is an RFC 9457 problem: the ones endpoints return through
    /// <see cref="ApiResults"/>, a status code with no body (an unknown route's 404), and an unhandled
    /// exception, which becomes a 500 with the code <c>internal.unexpected</c> and no detail; the
    /// exception is logged, never shown. ASP.NET Core adds the <c>traceId</c> to each one.
    /// </summary>
    public static IServiceCollection AddApiProblemDetails(this IServiceCollection services) =>
        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            context.ProblemDetails.Instance ??= context.HttpContext.Request.Path;
            if (context.Exception is not null)
            {
                context.ProblemDetails.Extensions["code"] = UnexpectedCode;
            }
            else if (!context.ProblemDetails.Extensions.ContainsKey("code")
                && context.ProblemDetails.Status is { } status && SignInProblems.TryGetValue(status, out var problem))
            {
                // Authentication and authorization answer with a bare status; say what it means.
                context.ProblemDetails.Detail ??= problem.Detail;
                context.ProblemDetails.Extensions["code"] = problem.Code;
            }
        });
}
