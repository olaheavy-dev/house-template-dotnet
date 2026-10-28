using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Common.Application.Idempotency;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Common.Presentation.Idempotency;

public static class IdempotencyApplicationExtensions
{
    /// <summary>Handles <c>Idempotency-Key</c> for the endpoints that opt in. Place it after authentication and authorization.</summary>
    public static IApplicationBuilder UseIdempotency(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseMiddleware<IdempotencyMiddleware>();
    }
}

/// <summary>
/// The Idempotency-Key HTTP header (IETF draft-ietf-httpapi-idempotency-key-header): the first request with a key
/// runs and its response is kept; the same request again gets that response back. A key belongs to the signed-in
/// user (or the caller's address) and to one request: method, path, query and body.
/// </summary>
internal sealed class IdempotencyMiddleware(RequestDelegate next, IOptions<IdempotencyOptions> options)
{
    public const string KeyRequiredCode = "idempotency.key_required";
    public const string KeyInvalidCode = "idempotency.key_invalid";
    public const string KeyReusedCode = "idempotency.key_reused";
    public const string InProgressCode = "idempotency.in_progress";

    internal const int MaxKeyLength = 255;
    private readonly int _maxResponseBytes = options.Value.MaxResponseBytes;

    public async Task InvokeAsync(HttpContext context, IIdempotencyStore store)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(store);
        var metadata = context.GetEndpoint()?.Metadata.GetMetadata<IdempotencyMetadata>();
        if (metadata is null || HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method) || HttpMethods.IsOptions(context.Request.Method))
        {
            await next(context);
            return;
        }

        // Present but empty counts as present, and invalid: a client that sent a key meant to be protected.
        var sent = context.Request.Headers.TryGetValue(IdempotencyEndpoints.HeaderName, out var header);
        if (!sent)
        {
            if (metadata.Required)
            {
                await ProblemAsync(context, StatusCodes.Status400BadRequest, KeyRequiredCode, $"This request needs an {IdempotencyEndpoints.HeaderName} header.");
                return;
            }

            await next(context);
            return;
        }

        var key = header.Count == 1 ? header[0] : null;
        if (!IsValidKey(key))
        {
            await ProblemAsync(context, StatusCodes.Status400BadRequest, KeyInvalidCode, $"{IdempotencyEndpoints.HeaderName} must be one value of 1 to {MaxKeyLength} printable characters.");
            return;
        }

        var scope = ScopeOf(context);
        var fingerprint = await FingerprintAsync(context);
        var cancellationToken = context.RequestAborted;
        var begin = await store.BeginAsync(scope, key!, fingerprint, cancellationToken);
        switch (begin.Outcome)
        {
            case IdempotencyOutcome.Replay:
                await ReplayAsync(context, begin.Response!);
                return;
            case IdempotencyOutcome.Mismatch:
                await ProblemAsync(context, StatusCodes.Status422UnprocessableEntity, KeyReusedCode, $"That {IdempotencyEndpoints.HeaderName} was used for a different request.");
                return;
            case IdempotencyOutcome.InProgress:
                context.Response.Headers.RetryAfter = "1";
                await ProblemAsync(context, StatusCodes.Status409Conflict, InProgressCode, "A request with that key is still running: retry shortly.");
                return;
        }

        await RunAndRememberAsync(context, store, scope, key!);
    }

    private async Task RunAndRememberAsync(HttpContext context, IIdempotencyStore store, string scope, string key)
    {
        var original = context.Response.Body;
        await using var captured = new MemoryStream();
        context.Response.Body = captured;
        try
        {
            await next(context);
        }
        catch
        {
            context.Response.Body = original;
            await store.AbandonAsync(scope, key, CancellationToken.None);
            throw;
        }

        context.Response.Body = original;
        var status = context.Response.StatusCode;
        // A server error says nothing about the request: let a retry run it again, as it must be able to.
        if (status >= StatusCodes.Status500InternalServerError || captured.Length > _maxResponseBytes)
        {
            await store.AbandonAsync(scope, key, CancellationToken.None);
        }
        else
        {
            await store.CompleteAsync(
                scope,
                key,
                new StoredResponse(status, context.Response.ContentType, context.Response.Headers.Location.ToString() is { Length: > 0 } location ? location : null, captured.ToArray()),
                CancellationToken.None);
        }

        captured.Position = 0;
        await captured.CopyToAsync(original, context.RequestAborted);
    }

    private static async Task ReplayAsync(HttpContext context, StoredResponse stored)
    {
        context.Response.StatusCode = stored.StatusCode;
        context.Response.ContentType = stored.ContentType;
        if (stored.Location is not null)
        {
            context.Response.Headers.Location = stored.Location;
        }

        context.Response.Headers[IdempotencyEndpoints.ReplayedHeaderName] = "true";
        await context.Response.Body.WriteAsync(stored.Body, context.RequestAborted);
    }

    private static async Task ProblemAsync(HttpContext context, int status, string code, string detail)
    {
        context.Response.StatusCode = status;
        await context.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails =
            {
                Status = status,
                Detail = detail,
                Extensions = { ["code"] = code },
            },
        });
    }

    private static bool IsValidKey(string? key) =>
        key is { Length: > 0 and <= MaxKeyLength } && key.All(character => character is >= '!' and <= '~');

    // The signed-in user's id; otherwise the caller's address (an unsigned endpoint has no one else to scope by).
    private static string ScopeOf(HttpContext context) =>
        context.User.FindFirst(SignedInUser.IdClaim)?.Value is { Length: > 0 } user
            ? "user:" + user
            : "address:" + context.Connection.RemoteIpAddress;

    // The request itself: what makes "the same request" the same. Path and query as sent, and the body's hash.
    private static async Task<string> FingerprintAsync(HttpContext context)
    {
        context.Request.EnableBuffering();
        var body = await SHA256.HashDataAsync(context.Request.Body, context.RequestAborted);
        context.Request.Body.Position = 0;
        var request = string.Create(CultureInfo.InvariantCulture, $"{context.Request.Method}\n{context.Request.GetEncodedPathAndQuery()}\n");
        return Convert.ToHexStringLower(SHA256.HashData([.. Encoding.UTF8.GetBytes(request), .. body]));
    }
}
