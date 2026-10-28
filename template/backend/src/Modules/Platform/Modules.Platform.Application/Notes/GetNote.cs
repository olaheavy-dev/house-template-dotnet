using ErrorOr;
using Immediate.Handlers.Shared;

namespace Modules.Platform.Application.Notes;

public sealed record GetNoteQuery(string OwnerId, Guid Id);

[Handler]
internal sealed partial class GetNote(INoteStore notes)
{
    private async ValueTask<ErrorOr<NoteResponse>> HandleAsync(GetNoteQuery query, CancellationToken cancellationToken)
    {
        var note = await notes.FindAsync(query.Id, cancellationToken);

        // Someone else's note is reported as missing, so ids can't be probed.
        return note is null || note.OwnerId != query.OwnerId
            ? Error.NotFound("note.not_found", "No such note.")
            : NoteResponse.From(note);
    }
}
