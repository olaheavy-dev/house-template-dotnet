using System.ComponentModel.DataAnnotations;
using JetBrains.Annotations;

namespace Api;

/// <summary>
/// Who issues the access tokens the API accepts, from the Auth section (Auth__Issuer and so on in the
/// environment). Unset Issuer means no tokens are accepted: every protected endpoint answers 401.
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)] // bound from configuration
internal sealed class AuthOptions
{
    public const string Section = "Auth";

    /// <summary>
    /// The issuer tokens must name, e.g. http://localhost:8080/realms/app for the local Keycloak or
    /// https://auth.example.com/realms/app. Its keys come from its OpenID configuration.
    /// </summary>
    public string? Issuer { get; init; }

    /// <summary>
    /// Where the API reads the issuer's OpenID configuration when that isn't the issuer's own
    /// address: in Compose the API reaches Keycloak at http://keycloak:8080 while tokens name
    /// localhost. Defaults to {Issuer}/.well-known/openid-configuration.
    /// </summary>
    public string? MetadataAddress { get; init; }

    /// <summary>The audience tokens must carry: the API's client in Keycloak.</summary>
    [Required]
    public string Audience { get; init; } = "api";

    /// <summary>Whether the issuer's configuration must come over HTTPS. Only a local Keycloak turns it off.</summary>
    public bool RequireHttpsMetadata { get; init; } = true;

    public bool AddressesAreAbsolute() =>
        new[] { Issuer, MetadataAddress }.All(address =>
            address is null || Uri.TryCreate(address, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http");
}
