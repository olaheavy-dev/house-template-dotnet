using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Testcontainers.Keycloak;

namespace IntegrationTests;

/// <summary>
/// Real tokens: Keycloak in a container with backend/keycloak/realm.json, and the API running as in
/// production, where only tokens count. Needs Docker.
/// </summary>
[NonParallelizable]
public sealed class KeycloakTests
{
    // The image backend/compose.yaml will run, pinned by digest.
    internal const string Image = "quay.io/keycloak/keycloak:26.8.1@sha256:a78d6b79572168cb25886b80a8b5b8d4ed4390743e3eecb810538683e209dd6b";
    internal const string ForgingClient = "forges-a-permission";
    private static readonly Uri Me = new("/v1/me", UriKind.Relative);

    private readonly HttpClient _tokens = new();
    private KeycloakContainer _keycloak = null!;
    private TestApi _api = null!;
    private string _issuer = null!;

    [OneTimeSetUp]
    public async Task StartKeycloakAndTheApi()
    {
        _keycloak = new KeycloakBuilder(Image)
            .WithRealm(Path.Combine(Backend.Directory, "keycloak", "realm.json"))
            .Build();
        await _keycloak.StartAsync();
        _issuer = new Uri(new Uri(_keycloak.GetBaseAddress()), "realms/app").ToString();
        await AddClientThatForgesAPermissionClaim();
        _api = new TestApi(_ => { }, "Production", new Dictionary<string, string>
        {
            ["Auth:Issuer"] = _issuer,
            ["Auth:RequireHttpsMetadata"] = "false",
        });
    }

    [OneTimeTearDown]
    public async Task StopThem()
    {
        await _api.DisposeAsync();
        await _keycloak.DisposeAsync();
        _tokens.Dispose();
    }

    [Test]
    public async Task A_users_token_shows_who_they_are_and_what_they_may_do()
    {
        using var client = await ClientSignedInAs("dev", "dev");

        using var response = await client.GetAsync(Me);
        using var me = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(Guid.TryParse(me.RootElement.GetProperty("id").GetString(), out _), Is.True, "the id is Keycloak's subject");
        Assert.That(me.RootElement.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()), Is.EqualTo(new[] { "flags.read", "me.read", "notes.read", "notes.write" }));
    }

    [Test]
    public async Task A_user_without_the_role_gets_a_403()
    {
        using var client = await ClientSignedInAs("visitor", "visitor");

        using var response = await client.GetAsync(Me);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task A_permission_claim_the_token_carries_itself_grants_nothing()
    {
        // Codex review on PR 9: a correctly signed token whose issuer adds a top-level permission
        // claim (a misconfigured mapper, another client) must not grant it; only the API client's roles do.
        using var client = _api.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", await TokenFor("visitor", "visitor", ForgingClient));

        using var response = await client.GetAsync(Me);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task A_tampered_token_gets_a_401()
    {
        var token = await TokenFor("dev", "dev");
        using var client = _api.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token[..^4] + "AAAA");

        using var response = await client.GetAsync(Me);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task A_token_for_another_audience_gets_a_401()
    {
        await using var other = new TestApi(_ => { }, "Production", new Dictionary<string, string>
        {
            ["Auth:Issuer"] = _issuer,
            ["Auth:RequireHttpsMetadata"] = "false",
            ["Auth:Audience"] = "another-api",
        });
        using var client = other.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await TokenFor("dev", "dev"));

        using var response = await client.GetAsync(Me);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task The_development_stand_in_counts_for_nothing_beside_real_sign_in()
    {
        using var client = _api.CreateClient();
        client.DefaultRequestHeaders.Add("X-Dev-User", "alice");

        using var response = await client.GetAsync(Me);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    // A client whose tokens carry the API audience and a hardcoded top-level permission claim.
    private async Task AddClientThatForgesAPermissionClaim()
    {
        using var adminForm = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["client_id"] = "admin-cli",
            ["username"] = KeycloakBuilder.DefaultUsername,
            ["password"] = KeycloakBuilder.DefaultPassword,
        });
        using var adminTokenResponse = await _tokens.PostAsync(
            new Uri(new Uri(_keycloak.GetBaseAddress()), "realms/master/protocol/openid-connect/token"), adminForm);
        using var adminToken = JsonDocument.Parse(await adminTokenResponse.Content.ReadAsStringAsync());
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(_keycloak.GetBaseAddress()), "admin/realms/app/clients"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken.RootElement.GetProperty("access_token").GetString());
        request.Content = JsonContent(new
        {
            clientId = ForgingClient,
            publicClient = true,
            directAccessGrantsEnabled = true,
            standardFlowEnabled = false,
            protocolMappers = new[]
            {
                Mapper("api audience", "oidc-audience-mapper", new Dictionary<string, string>
                {
                    ["included.client.audience"] = "api",
                    ["access.token.claim"] = "true",
                }),
                Mapper("forged permission", "oidc-hardcoded-claim-mapper", new Dictionary<string, string>
                {
                    ["claim.name"] = "permission",
                    ["claim.value"] = "me.read",
                    ["jsonType.label"] = "String",
                    ["access.token.claim"] = "true",
                }),
            },
        });

        using var response = await _tokens.SendAsync(request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created), await response.Content.ReadAsStringAsync());
    }

    private static object Mapper(string name, string kind, Dictionary<string, string> config) =>
        new { name, protocol = "openid-connect", protocolMapper = kind, config };

    private static StringContent JsonContent(object value) =>
        new(JsonSerializer.Serialize(value), System.Text.Encoding.UTF8, new MediaTypeHeaderValue("application/json"));

    private async Task<HttpClient> ClientSignedInAs(string username, string password)
    {
        var client = _api.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await TokenFor(username, password));
        return client;
    }

    // The realm's local-cli client allows the password grant, for curl and these tests only.
    private async Task<string> TokenFor(string username, string password, string clientId = "local-cli")
    {
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["client_id"] = clientId,
            ["username"] = username,
            ["password"] = password,
        });
        using var response = await _tokens.PostAsync(new Uri($"{_issuer}/protocol/openid-connect/token"), form);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
        using var token = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return token.RootElement.GetProperty("access_token").GetString()!;
    }
}
