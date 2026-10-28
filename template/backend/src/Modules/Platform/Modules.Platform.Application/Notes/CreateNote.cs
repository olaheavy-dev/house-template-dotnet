using ErrorOr;
using FluentValidation;
using Immediate.Handlers.Shared;
using Modules.Platform.Domain;
using Modules.Platform.IntegrationEvents;

namespace Modules.Platform.Application.Notes;

public sealed record CreateNoteCommand(string OwnerId, string Text);

public sealed record NoteResponse(Guid Id, string Text, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt)
{
    internal static NoteResponse From(Note note) => new(note.Id, note.Text, note.CreatedAt, note.UpdatedAt);
}

internal sealed class CreateNoteValidator : AbstractValidator<CreateNoteCommand>
{
    public CreateNoteValidator()
    {
        RuleFor(command => command.OwnerId).NotEmpty();
        // A body of {} binds a null text, whatever the type says.
        RuleFor(command => command.Text).NotNull();
    }
}

[Handler]
internal sealed partial class CreateNote(INoteStore notes, TimeProvider clock)
{
    private async ValueTask<ErrorOr<NoteResponse>> HandleAsync(CreateNoteCommand command, CancellationToken cancellationToken)
    {
        var created = Note.Create(Guid.CreateVersion7(clock.GetUtcNow()), command.OwnerId, command.Text);
        if (created.IsError)
        {
            return created.Errors;
        }

        await notes.AddAsync(created.Value, cancellationToken);
        // Stored with the note, delivered once it is committed.
        await notes.PublishAsync(new NoteCreated(created.Value.Id, created.Value.OwnerId, clock.GetUtcNow()), cancellationToken);
        await notes.SaveChangesAsync(cancellationToken);
        return NoteResponse.From(created.Value);
    }
}
