namespace Modules.Platform.Presentation;

/// <summary>The Platform module's feature flags, defined under <c>feature_management</c> in configuration.</summary>
public static class PlatformFlags
{
    /// <summary>The notes list. On by default; switch it off to take the endpoint away (404) without a deploy.</summary>
    public const string NotesList = "notes-list";
}
