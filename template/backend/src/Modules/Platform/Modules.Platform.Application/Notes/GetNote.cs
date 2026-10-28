using ErrorOr;
using Immediate.Handlers.Shared;
using Microsoft.Extensions.Caching.Hybrid;

namespace Modules.Platform.Application.Notes;

public sealed record GetNoteQuery(string OwnerId, Guid Id);

/// <summary>
/// One note by id, through the cache: the first read of an id goes to the store, the rest are answered from
/// memory (and from the shared cache, when there is one) until the entry expires, and concurrent first reads
/// share one trip to the store. Notes never change, so expiry is all it needs; a handler that changes what
/// it caches calls <c>cache.RemoveAsync(key)</c> (or <c>RemoveByTagAsync</c>) after it saves.
/// </summary>
[Handler]
internal sealed partial class GetNote(INoteStore notes, HybridCache cache)
{
    private async ValueTask<ErrorOr<NoteResponse>> HandleAsync(GetNoteQuery query, CancellationToken cancellationToken)
    {
        // The key says what is cached, not who asked: the owner check below is made on every read.
        var key = $"platform:note:{query.Id}";
        var note = await cache.GetOrCreateAsync(
            key,
            async token =>
            {
                var stored = await notes.FindAsync(query.Id, token);
                return stored is null ? null : new CachedNote(stored.Id, stored.OwnerId, stored.Text, stored.CreatedAt, stored.UpdatedAt);
            },
            cancellationToken: cancellationToken);

        // A miss is not worth remembering: the note may be there a moment later, and the cache would hide it
        // until the entry expired. Reads that arrived together still shared the one trip to the store.
        if (note is null)
        {
            await cache.RemoveAsync(key, cancellationToken);
        }

        // Someone else's note is reported as missing, so ids can't be probed.
        return note is null || note.OwnerId != query.OwnerId
            ? Error.NotFound("note.not_found", "No such note.")
            : new NoteResponse(note.Id, note.Text, note.CreatedAt, note.UpdatedAt);
    }

    /// <summary>What the cache holds: plain data that serializes, not the entity.</summary>
    private sealed record CachedNote(Guid Id, string OwnerId, string Text, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
}
