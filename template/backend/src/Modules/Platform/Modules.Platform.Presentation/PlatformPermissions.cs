namespace Modules.Platform.Presentation;

/// <summary>The Platform module's permissions: the API client's roles in Keycloak (backend/keycloak/realm.json).</summary>
public static class PlatformPermissions
{
    /// <summary>See your own account.</summary>
    public const string ReadMe = "me.read";

    /// <summary>See the public feature flags as you are shown them.</summary>
    public const string ReadFlags = "flags.read";

    /// <summary>See your own notes.</summary>
    public const string ReadNotes = "notes.read";

    /// <summary>Write notes.</summary>
    public const string WriteNotes = "notes.write";
}
