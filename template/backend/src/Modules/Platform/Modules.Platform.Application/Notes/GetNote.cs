using ErrorOr;
using Immediate.Handlers.Shared;
using Microsoft.Extensions.Caching.Hybrid;

namespace Modules.Platform.Application.Notes;

public sealed record GetNoteQuery(string OwnerId, Guid Id);

/// <summary>
/// One note by id, through the cache: the first read of an id goes to the store, the rest are answered from
/// memory (and from the shared cache, when there is one) until the entry expires, and concurrent first reads
/// share one trip to the store. A note that is not there is never cached. Notes never change, so expiry is all it needs; a handler that changes what
/// it caches calls <c>cache.RemoveAsync(key)</c> (or <c>RemoveByTagAsync</c>) after it saves. The lookup runs once
/// for every request waiting on a key and may outlive the request that started it, so it uses an
/// <see cref="INoteReader"/> (its own scope), never the request's store.
/// </summary>
[Handler]
internal sealed partial class GetNote(INoteReader notes, HybridCache cache)
{
    private async ValueTask<ErrorOr<NoteResponse>> HandleAsync(GetNoteQuery query, CancellationToken cancellationToken)
    {
        // The key says what is cached, not who asked: the owner check below is made on every read.
        var key = NoteCache.KeyFor(query.Id);
        CachedNote note;
        try
        {
            note = await cache.GetOrCreateAsync(
                key,
                async token =>
                {
                    var stored = await notes.FindAsync(query.Id, token);

                    // A miss is not worth remembering: the note may be there a moment later, and the cache would hide it
                    // until the entry expired. The cache stores nothing when its factory throws (and shares the exception
                    // with the reads that arrived together), so a miss leaves no entry. Returning null and removing the
                    // entry afterwards is not enough: the cache writes to the shared level after it has answered, and
                    // that write can land after the removal.
                    return stored is null
                        ? throw new NoteNotFoundException()
                        : new CachedNote(stored.Id, stored.OwnerId, stored.Text, stored.CreatedAt, stored.UpdatedAt);
                },
                cancellationToken: cancellationToken);
        }
        catch (NoteNotFoundException)
        {
            return Error.NotFound("note.not_found", "No such note.");
        }

        // Someone else's note is reported as missing, so ids can't be probed.
        return note.OwnerId != query.OwnerId
            ? Error.NotFound("note.not_found", "No such note.")
            : new NoteResponse(note.Id, note.Text, note.CreatedAt, note.UpdatedAt);
    }

    /// <summary>Thrown by the cache lookup for a note that is not there, so the cache stores nothing. Never leaves this class.</summary>
    public sealed class NoteNotFoundException : Exception
    {
        public NoteNotFoundException()
        {
        }

        public NoteNotFoundException(string message)
            : base(message)
        {
        }

        public NoteNotFoundException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    /// <summary>What the cache holds: plain data that serializes, not the entity.</summary>
    private sealed record CachedNote(Guid Id, string OwnerId, string Text, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
}
