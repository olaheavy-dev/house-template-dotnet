using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace ServiceDefaults;

/// <summary>
/// Gives every request an id: the caller's X-Request-ID (the Next.js proxy forwards one) when it
/// looks like an id, otherwise a new one. It is echoed in the response, the 500 problem included,
/// and every log line for the request carries it as <c>RequestId</c>, next to the <c>TraceId</c>.
/// The same contract as the FastAPI layer. No maintained package does this; the best known,
/// CorrelationId, has had no release since 2020.
/// </summary>
internal sealed partial class RequestIds(RequestDelegate next, ILogger<RequestIds> logger)
{
    public const string Header = "X-Request-ID";

    private static readonly Func<ILogger, string, IDisposable?> Scope =
        LoggerMessage.DefineScope<string>("RequestId:{RequestId}");

    public async Task InvokeAsync(HttpContext context)
    {
        var incoming = context.Request.Headers[Header].ToString();
        var id = SafeId().IsMatch(incoming) ? incoming : Guid.CreateVersion7().ToString("N");
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[Header] = id;
            return Task.CompletedTask;
        });
        using (Scope(logger, id))
        {
            await next(context);
        }
    }

    // Only what an id looks like, so a header can't put arbitrary text into the logs.
    [GeneratedRegex("^[A-Za-z0-9._-]{1,128}$")]
    private static partial Regex SafeId();
}
