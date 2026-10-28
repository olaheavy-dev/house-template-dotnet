namespace Common.Application.Idempotency;

/// <summary>What a repeated request gets back: the response the first one produced.</summary>
public sealed record StoredResponse(int StatusCode, string? ContentType, string? Location, ReadOnlyMemory<byte> Body);

public enum IdempotencyOutcome
{
    /// <summary>First time (or the earlier attempt gave up): run the request, then <c>CompleteAsync</c>.</summary>
    Started,

    /// <summary>The same request already finished: answer with <see cref="IdempotencyBegin.Response"/>.</summary>
    Replay,

    /// <summary>The same request is still running (409).</summary>
    InProgress,

    /// <summary>The key was used for a different request (422).</summary>
    Mismatch,
}

/// <param name="Outcome">What to do with the request.</param>
/// <param name="Response">For <see cref="IdempotencyOutcome.Replay"/>: the response to send again.</param>
/// <param name="Claim">For <see cref="IdempotencyOutcome.Started"/>: proof that this request holds the key. Only the holder can complete or abandon it, so a slow first attempt cannot disturb the retry that took over from it.</param>
public sealed record IdempotencyBegin(IdempotencyOutcome Outcome, StoredResponse? Response = null, Guid? Claim = null);

/// <summary>
/// Remembers requests by <c>Idempotency-Key</c> so a client can retry a POST without doing it twice.
/// A key belongs to one <c>scope</c> (the signed-in user) and one request fingerprint.
/// </summary>
public interface IIdempotencyStore
{
    Task<IdempotencyBegin> BeginAsync(string scope, string key, string fingerprint, CancellationToken cancellationToken);

    /// <summary>Keeps the response for replays until the key expires. Does nothing unless <paramref name="claim"/> still holds the key.</summary>
    Task CompleteAsync(string scope, string key, Guid claim, StoredResponse response, CancellationToken cancellationToken);

    /// <summary>Forgets a started request that failed, so a retry runs it again. Does nothing unless <paramref name="claim"/> still holds the key.</summary>
    Task AbandonAsync(string scope, string key, Guid claim, CancellationToken cancellationToken);
}
