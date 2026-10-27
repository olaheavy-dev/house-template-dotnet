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
        Assert.That(me.RootElement.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()), Is.EqualTo(new[] { "me.read" }));
    }

    [Test]
    public async Task A_user_without_the_role_gets_a_403()
    {
        using var client = await ClientSignedInAs("visitor", "visitor");

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

    private async Task<HttpClient> ClientSignedInAs(string username, string password)
    {
        var client = _api.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await TokenFor(username, password));
        return client;
    }

    // The realm's local-cli client allows the password grant, for curl and these tests only.
    private async Task<string> TokenFor(string username, string password)
    {
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["client_id"] = "local-cli",
            ["username"] = username,
            ["password"] = password,
        });
        using var response = await _tokens.PostAsync(new Uri($"{_issuer}/protocol/openid-connect/token"), form);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
        using var token = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return token.RootElement.GetProperty("access_token").GetString()!;
    }
}
