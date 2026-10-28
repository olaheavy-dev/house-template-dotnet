using Common.Application.Idempotency;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Common.Infrastructure.Idempotency;

/// <summary>
/// Idempotency keys in Postgres. A key is claimed with one <c>INSERT ... ON CONFLICT DO NOTHING</c>,
/// so two requests racing for it cannot both start; the loser reads what the winner left.
/// </summary>
internal sealed class PostgresIdempotencyStore(CommonDbContext db, TimeProvider clock, IOptions<IdempotencyOptions> options) : IIdempotencyStore
{
    internal const int DefaultStatus = 200;

    private readonly IdempotencyOptions _options = options.Value;

    public async Task<IdempotencyBegin> BeginAsync(string scope, string key, string fingerprint, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();

        // A key past its time is as good as new.
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""DELETE FROM common.idempotency_keys WHERE "Scope" = {scope} AND "Key" = {key} AND "ExpiresAt" <= {now}""",
            cancellationToken);

        var claim = Guid.NewGuid();
        var inserted = await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO common.idempotency_keys ("Scope", "Key", "Fingerprint", "State", "ClaimToken", "LockedUntil", "ExpiresAt", "CreatedAt")
            VALUES ({scope}, {key}, {fingerprint}, {IdempotencyRecord.Started}, {claim}, {now.AddSeconds(_options.LockSeconds)}, {now.AddHours(_options.KeyTtlHours)}, {now})
            ON CONFLICT ("Scope", "Key") DO NOTHING
            """,
            cancellationToken);
        if (inserted == 1)
        {
            return new IdempotencyBegin(IdempotencyOutcome.Started, Claim: claim);
        }

        var existing = await db.IdempotencyKeys.AsNoTracking()
            .SingleOrDefaultAsync(record => record.Scope == scope && record.Key == key, cancellationToken);
        if (existing is null)
        {
            // Gone between the insert and the read (abandoned, or expired): the caller may try again.
            return new IdempotencyBegin(IdempotencyOutcome.InProgress);
        }

        if (existing.Fingerprint != fingerprint)
        {
            return new IdempotencyBegin(IdempotencyOutcome.Mismatch);
        }

        if (existing.State == IdempotencyRecord.Completed)
        {
            return new IdempotencyBegin(
                IdempotencyOutcome.Replay,
                new StoredResponse(existing.StatusCode ?? DefaultStatus, existing.ContentType, existing.Location, existing.Body ?? []));
        }

        // Still running, unless its hold has run out: then the first attempt probably died, and this one takes the key over.
        var tookOver = await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE common.idempotency_keys SET "LockedUntil" = {now.AddSeconds(_options.LockSeconds)}, "ClaimToken" = {claim}
            WHERE "Scope" = {scope} AND "Key" = {key} AND "State" = {IdempotencyRecord.Started} AND "LockedUntil" <= {now}
            """,
            cancellationToken);
        return tookOver == 1
            ? new IdempotencyBegin(IdempotencyOutcome.Started, Claim: claim)
            : new IdempotencyBegin(IdempotencyOutcome.InProgress);
    }

    public async Task CompleteAsync(string scope, string key, Guid claim, StoredResponse response, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(response);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE common.idempotency_keys
            SET "State" = {IdempotencyRecord.Completed}, "StatusCode" = {response.StatusCode}, "ContentType" = {response.ContentType},
                "Location" = {response.Location}, "Body" = {response.Body.ToArray()}, "LockedUntil" = NULL
            WHERE "Scope" = {scope} AND "Key" = {key} AND "State" = {IdempotencyRecord.Started} AND "ClaimToken" = {claim}
            """,
            cancellationToken);
    }

    public async Task AbandonAsync(string scope, string key, Guid claim, CancellationToken cancellationToken) =>
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""DELETE FROM common.idempotency_keys WHERE "Scope" = {scope} AND "Key" = {key} AND "State" = {IdempotencyRecord.Started} AND "ClaimToken" = {claim}""",
            cancellationToken);
}
