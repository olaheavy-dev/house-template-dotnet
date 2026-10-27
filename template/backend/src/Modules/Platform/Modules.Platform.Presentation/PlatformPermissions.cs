namespace Modules.Platform.Presentation;

/// <summary>The Platform module's permissions: the API client's roles in Keycloak (backend/keycloak/realm.json).</summary>
public static class PlatformPermissions
{
    /// <summary>See your own account.</summary>
    public const string ReadMe = "me.read";
}
