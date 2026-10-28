using Microsoft.Extensions.Logging;
using Modules.Platform.IntegrationEvents;

namespace Modules.Platform.Infrastructure.Subscribers;

/// <summary>
/// The example subscriber every new one copies: receives <see cref="NoteCreated"/> once the note is
/// committed, at least once (a redelivery is dropped by the inbox, but handlers should still be safe to
/// repeat). Public with a `Handle` method named by convention, because the host generates the code that calls it.
/// </summary>
public sealed partial class NoteCreatedConsumer
{
    public static void Handle(NoteCreated message, ILogger<NoteCreatedConsumer> logger)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(logger);
        LogNoteCreated(logger, message.NoteId);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "A note was created: {NoteId}")]
    private static partial void LogNoteCreated(ILogger logger, Guid noteId);
}
