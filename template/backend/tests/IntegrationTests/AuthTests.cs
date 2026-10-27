using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Api;

namespace IntegrationTests;

/// <summary>
/// Who may call GET /v1/me, without a real issuer: anonymous callers get 401, the development
/// stand-in (X-Dev-User) works in Development only, and a missing permission is a 403.
/// KeycloakTests covers real tokens.
/// </summary>
public sealed class AuthTests
{
    private static readonly Uri Me = new("/v1/me", UriKind.Relative);

    [Test]
    public async Task An_anonymous_caller_gets_a_401_problem()
    {
        await using var api = new TestApi(_ => { });
        using var client = api.CreateClient();

        using var response = await client.GetAsync(Me);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(response.Headers.WwwAuthenticate.Select(header => header.Scheme), Does.Contain("Bearer"));
        Assert.That(problem.RootElement.GetProperty("code").GetString(), Is.EqualTo("auth.unauthenticated"));
    }

    [Test]
    public async Task The_development_stand_in_is_signed_in_with_every_permission()
    {
        await using var api = new TestApi(_ => { });
        using var client = api.CreateClient();
        client.DefaultRequestHeaders.Add("X-Dev-User", "alice");

        using var response = await client.GetAsync(Me);
        using var me = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(me.RootElement.GetProperty("id").GetString(), Is.EqualTo("alice"));
        Assert.That(me.RootElement.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()), Is.EqualTo(new[] { "*" }));
    }

    [Test]
    public async Task A_user_without_the_permission_gets_a_403_problem()
    {
        await using var api = new TestApi(_ => { });
        using var client = api.CreateClient();
        client.DefaultRequestHeaders.Add("X-Dev-User", "alice");
        client.DefaultRequestHeaders.Add("X-Dev-Permissions", "other.read");

        using var response = await client.GetAsync(Me);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That(problem.RootElement.GetProperty("code").GetString(), Is.EqualTo("auth.forbidden"));
    }

    [Test]
    public async Task The_stand_in_with_the_permission_listed_is_allowed()
    {
        await using var api = new TestApi(_ => { });
        using var client = api.CreateClient();
        client.DefaultRequestHeaders.Add("X-Dev-User", "alice");
        client.DefaultRequestHeaders.Add("X-Dev-Permissions", "other.read, me.read");

        using var response = await client.GetAsync(Me);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [TestCase("has space")]
    [TestCase("")]
    public async Task An_unsafe_stand_in_id_is_not_signed_in(string id)
    {
        await using var api = new TestApi(_ => { });
        using var client = api.CreateClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Dev-User", id);

        using var response = await client.GetAsync(Me);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Outside_development_the_stand_in_is_ignored()
    {
        await using var api = new TestApi("Production");
        using var client = api.CreateClient();
        client.DefaultRequestHeaders.Add("X-Dev-User", "alice");

        using var response = await client.GetAsync(Me);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task A_bearer_token_wins_over_the_stand_in()
    {
        // A request with a token is judged by the token, never by the header beside it.
        await using var api = new TestApi(_ => { });
        using var client = api.CreateClient();
        client.DefaultRequestHeaders.Add("X-Dev-User", "alice");
        client.DefaultRequestHeaders.Authorization = new("Bearer", "not-a-token");

        using var response = await client.GetAsync(Me);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Each_signed_in_user_has_their_own_rate_limit()
    {
        await using var api = new TestApi(_ => { }, settings: new Dictionary<string, string> { ["Api:RateLimitPerMinute"] = "1" });
        using var alice = api.CreateClient();
        using var bob = api.CreateClient();
        alice.DefaultRequestHeaders.Add("X-Dev-User", "alice");
        bob.DefaultRequestHeaders.Add("X-Dev-User", "bob");

        using var aliceFirst = await alice.GetAsync(Me);
        using var aliceSecond = await alice.GetAsync(Me);
        using var bobFirst = await bob.GetAsync(Me);

        Assert.That(aliceSecond.StatusCode, Is.EqualTo(HttpStatusCode.TooManyRequests));
        Assert.That(bobFirst.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [TestCase("""{"api":{"roles":["me.read","files.write"]},"account":{"roles":["manage-account"]}}""", new[] { "me.read", "files.write" })]
    [TestCase("""{"account":{"roles":["me.read"]}}""", new string[0])]
    [TestCase("""{"api":{"roles":["*"]}}""", new string[0])]
    [TestCase("""{"api":{"roles":"me.read"}}""", new string[0])]
    [TestCase("""[]""", new string[0])]
    public void Only_the_api_clients_roles_become_permissions(string resourceAccess, string[] expected)
    {
        var identity = new ClaimsIdentity([new Claim("resource_access", resourceAccess)], "Bearer");

        Auth.AddPermissions(identity, "api");

        Assert.That(identity.FindAll("permission").Select(claim => claim.Value), Is.EqualTo(expected));
    }
}
