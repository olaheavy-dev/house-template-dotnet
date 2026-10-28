using Microsoft.AspNetCore.Builder;

namespace Common.Presentation.Idempotency;

/// <summary>Marks an endpoint as taking an <c>Idempotency-Key</c>. See <see cref="IdempotencyEndpoints.WithIdempotencyKey{TBuilder}"/>.</summary>
public sealed record IdempotencyMetadata(bool Required);

public static class IdempotencyEndpoints
{
    public const string HeaderName = "Idempotency-Key";
    public const string ReplayedHeaderName = "Idempotency-Replayed";

    /// <summary>
    /// Lets a client retry this endpoint safely: send the same <c>Idempotency-Key</c> and the request runs once;
    /// repeats get the first response back (with <c>Idempotency-Replayed: true</c>). The same key with a different
    /// request is a 422, one that is still running a 409. With <paramref name="required"/>, a request without a key is a 400.
    /// For POST and other unsafe methods; GET, HEAD and OPTIONS pass through. Needs <c>app.UseIdempotency()</c>.
    /// </summary>
    public static TBuilder WithIdempotencyKey<TBuilder>(this TBuilder builder, bool required = false)
        where TBuilder : IEndpointConventionBuilder =>
        builder.WithMetadata(new IdempotencyMetadata(required));
}
