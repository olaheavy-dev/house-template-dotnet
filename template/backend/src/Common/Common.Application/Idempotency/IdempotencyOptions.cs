using System.ComponentModel.DataAnnotations;

namespace Common.Application.Idempotency;

/// <summary>Settings under <c>Idempotency</c>.</summary>
public sealed class IdempotencyOptions
{
    public const string Section = "Idempotency";

    /// <summary>How long a key (and the response kept for it) is remembered.</summary>
    [Range(1, 168)]
    public int KeyTtlHours { get; init; } = 24;

    /// <summary>How long a running request holds its key; after that a retry may take over (the first attempt probably died).</summary>
    [Range(5, 3600)]
    public int LockSeconds { get; init; } = 60;

    /// <summary>Responses larger than this are not kept, so their retries run again.</summary>
    [Range(1024, 10_485_760)]
    public int MaxResponseBytes { get; init; } = 1_048_576;
}
