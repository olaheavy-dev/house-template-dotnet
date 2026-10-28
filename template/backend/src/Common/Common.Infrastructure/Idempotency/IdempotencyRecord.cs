using System.Diagnostics.CodeAnalysis;

namespace Common.Infrastructure.Idempotency;

/// <summary>One remembered request: who sent it, under which key, what it was, and (once done) what it got.</summary>
public sealed class IdempotencyRecord
{
    public const string Started = "started";
    public const string Completed = "completed";

    public string Scope { get; init; } = string.Empty;

    public string Key { get; init; } = string.Empty;

    public string Fingerprint { get; init; } = string.Empty;

    public string State { get; set; } = Started;

    /// <summary>Who holds the key while it is started; a takeover replaces it, so the earlier holder can no longer complete or abandon.</summary>
    public Guid ClaimToken { get; set; }

    public int? StatusCode { get; set; }

    public string? ContentType { get; set; }

    public string? Location { get; set; }

    [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "EF Core maps a bytea column to byte[].")]
    public byte[]? Body { get; set; }

    /// <summary>While a request runs: when its hold on the key ends.</summary>
    public DateTimeOffset? LockedUntil { get; set; }

    public DateTimeOffset ExpiresAt { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}
