namespace Modules.Platform.IntegrationEvents;

/// <summary>A user saved a note. Published when the note is stored; other modules may subscribe.</summary>
public sealed record NoteCreated(Guid NoteId, string OwnerId, DateTimeOffset CreatedAt);
