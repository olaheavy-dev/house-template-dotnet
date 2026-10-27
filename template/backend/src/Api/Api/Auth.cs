using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using Common.Infrastructure;
using Common.Presentation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace Api;

/// <summary>
/// Who is calling. A bearer token from the configured issuer (Keycloak by default; backend/keycloak
/// holds the local realm), whose API-client roles become permissions. In Development only, an
/// X-Dev-User header stands in for a signed-in user, as in the FastAPI layer, so the app can be
/// built and tested before sign-in exists; the Next.js layer's DEV_USER sends it. With no issuer
/// configured, every protected endpoint answers 401.
/// </summary>
internal static partial class Auth
{
    public const string DevUserHeader = "X-Dev-User";

    /// <summary>The dev user's permissions, comma-separated; without it they hold every permission.</summary>
    public const string DevPermissionsHeader = "X-Dev-Permissions";

    /// <summary>The default scheme: a bearer token, or in Development the X-Dev-User stand-in.</summary>
    public const string BearerOrDevUser = "BearerOrDevUser";

    public static IServiceCollection AddAuth(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddValidatedOptions<AuthOptions>(configuration.GetSection(AuthOptions.Section))
            .Validate(options => options.AddressesAreAbsolute(), "Auth:Issuer and Auth:MetadataAddress must be absolute http(s) URLs.");

        var development = environment.IsDevelopment();
        var authentication = services.AddAuthentication(BearerOrDevUser)
            .AddPolicyScheme(BearerOrDevUser, null, options => options.ForwardDefaultSelector = context =>
                development && context.Request.Headers.ContainsKey(DevUserHeader)
                    && !context.Request.Headers.ContainsKey(HeaderNames.Authorization)
                    ? DevUser.SchemeName
                    : JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();
        if (development)
        {
            authentication.AddScheme<AuthenticationSchemeOptions, DevUser>(DevUser.SchemeName, null);
        }

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme).Configure<IOptions<AuthOptions>>((jwt, auth) =>
        {
            jwt.Authority = auth.Value.Issuer;
            jwt.MetadataAddress = auth.Value.MetadataAddress ?? string.Empty;
            jwt.Audience = auth.Value.Audience;
            jwt.RequireHttpsMetadata = auth.Value.RequireHttpsMetadata;
            jwt.TokenValidationParameters.ValidIssuer = auth.Value.Issuer;
            // Claims keep their token names (sub, not a .NET URI), and the subject names the user.
            jwt.MapInboundClaims = false;
            jwt.TokenValidationParameters.NameClaimType = SignedInUser.IdClaim;
            jwt.Events = new JwtBearerEvents
            {
                OnTokenValidated = context =>
                {
                    if (context.Principal?.Identity is ClaimsIdentity identity)
                    {
                        AddPermissions(identity, auth.Value.Audience);
                    }

                    return Task.CompletedTask;
                },
            };
        });

        services.AddAuthorization();
        return services;
    }

    /// <summary>
    /// Keycloak puts a client's roles in resource_access.{client}.roles; the API client's roles are
    /// the permissions, and nothing else is: a permission claim in the token itself is dropped. Another
    /// issuer carries them elsewhere (Auth0: a permissions claim): change this to read it. A role
    /// named * is ignored: only the development stand-in holds every permission.
    /// </summary>
    internal static void AddPermissions(ClaimsIdentity identity, string audience)
    {
        // Only the API client's roles grant permissions: a permission claim the token carries itself
        // (a misconfigured mapper, a token meant for something else) is dropped first.
        foreach (var claimed in identity.FindAll(SignedInUser.PermissionClaim).ToList())
        {
            identity.RemoveClaim(claimed);
        }

        if (identity.FindFirst("resource_access")?.Value is not { } resourceAccess)
        {
            return;
        }

        using var json = JsonDocument.Parse(resourceAccess);
        if (json.RootElement.ValueKind != JsonValueKind.Object
            || !json.RootElement.TryGetProperty(audience, out var client)
            || client.ValueKind != JsonValueKind.Object
            || !client.TryGetProperty("roles", out var roles)
            || roles.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var role in roles.EnumerateArray())
        {
            if (role.ValueKind == JsonValueKind.String && role.GetString() is { Length: > 0 } permission && permission != SignedInUser.AllPermissions)
            {
                identity.AddClaim(new Claim(SignedInUser.PermissionClaim, permission));
            }
        }
    }

    /// <summary>The development stand-in: X-Dev-User names the user (an id, never trusted outside Development).</summary>
    private sealed partial class DevUser(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "DevUser";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var claimed = Request.Headers[DevUserHeader].ToString();
            if (!SafeId().IsMatch(claimed))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var permissions = Request.Headers.TryGetValue(DevPermissionsHeader, out var listed)
                ? listed.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                : [SignedInUser.AllPermissions];
            var identity = new ClaimsIdentity(
                [new Claim(SignedInUser.IdClaim, claimed), .. permissions.Select(permission => new Claim(SignedInUser.PermissionClaim, permission))],
                SchemeName,
                SignedInUser.IdClaim,
                roleType: null);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }

        // The FastAPI layer's rule: what an id looks like, so the header can't smuggle anything else.
        [GeneratedRegex("^[A-Za-z0-9._:@-]{1,200}$")]
        private static partial Regex SafeId();
    }
}
