using Common.Application.Paging;
using ErrorOr;
using FluentValidation;
using Immediate.Handlers.Shared;

namespace Modules.Platform.Application.Notes;

public sealed record ListNotesQuery(string OwnerId, PageRequest Page);

internal sealed class ListNotesValidator : AbstractValidator<ListNotesQuery>
{
    public ListNotesValidator() => RuleFor(query => query.Page).SetValidator(new PageRequestValidator());
}

/// <summary>The user's notes, newest first, a page at a time. The example list every new one copies.</summary>
[Handler]
internal sealed partial class ListNotes(INoteStore notes)
{
    private async ValueTask<ErrorOr<Page<NoteResponse>>> HandleAsync(ListNotesQuery query, CancellationToken cancellationToken)
    {
        // Notes have time-ordered ids (UUIDv7), so the id is the sort key and the cursor.
        var before = Cursor.Decode<Guid>(query.Page.Cursor);
        if (before.IsError)
        {
            return before.Errors;
        }

        // One more than the page: it only says there is a next page.
        var rows = await notes.ListNewestAsync(query.OwnerId, before.Value, query.Page.Limit + 1, cancellationToken);
        return Page.From(rows, query.Page, note => note.Id, NoteResponse.From);
    }
}
