using Common.Domain;
using ErrorOr;

namespace Modules.Platform.Domain;

/// <summary>A short text a signed-in user keeps. The example stored entity every new one copies.</summary>
public sealed class Note : Entity
{
    public const int MaxLength = 500;

    private Note(Guid id, string ownerId, string text)
    {
        Id = id;
        OwnerId = ownerId;
        Text = text;
    }

    /// <summary>The user's id: the identity provider's subject.</summary>
    public string OwnerId { get; private set; }

    public string Text { get; private set; }

    public static ErrorOr<Note> Create(Guid id, string ownerId, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Validate(text).Then(trimmed => new Note(id, ownerId, trimmed));
    }

    public ErrorOr<Success> Edit(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Validate(text).Then(trimmed =>
        {
            Text = trimmed;
            return Result.Success;
        });
    }

    private static ErrorOr<string> Validate(string text)
    {
        var trimmed = text.Trim();
        return trimmed.Length is > 0 and <= MaxLength
            ? trimmed
            : Error.Validation("note.text", $"A note's text is 1 to {MaxLength} characters.");
    }
}
