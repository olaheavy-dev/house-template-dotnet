using System.Net;

namespace IntegrationTests;

/// <summary>Every response carries the API security headers; the development API reference gets the page set.</summary>
public sealed class SecurityHeadersTests
{
    [Test]
    public async Task An_api_response_has_the_api_headers()
    {
        await using var api = new TestApi(_ => { });
        using var client = api.CreateClient();

        using var response = await client.GetAsync(new Uri("/v1/platform/ping", UriKind.Relative));

        AssertApiHeaders(response);
    }

    [Test]
    public async Task Kestrel_names_no_server()
    {
        // The in-memory test server never sends one; Kestrel does unless told not to.
        await using var api = new TestApi(_ => { });
        api.UseKestrel(0);
        api.StartServer();
        using var client = api.CreateClient();

        using var response = await client.GetAsync(new Uri("/v1/platform/ping", UriKind.Relative));

        Assert.That(response.Headers.Contains("Server"), Is.False);
        AssertApiHeaders(response);
    }

    [Test]
    public async Task A_500_problem_has_them_too()
    {
        await using var api = PingHandlerStub.Api(() => throw new InvalidOperationException("boom"));
        using var client = api.CreateClient();

        using var response = await client.GetAsync(new Uri("/v1/platform/ping", UriKind.Relative));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
        AssertApiHeaders(response);
    }

    [Test]
    public async Task The_development_api_reference_may_run_its_scripts()
    {
        await using var api = new TestApi("Development");
        using var client = api.CreateClient();

        using var response = await client.GetAsync(new Uri("/scalar", UriKind.Relative));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(response.Headers.GetValues("Content-Security-Policy").Single(), Does.Not.Contain("default-src 'none'"));
        Assert.That(response.Headers.GetValues("X-Content-Type-Options"), Is.EqualTo(new[] { "nosniff" }));
    }

    private static void AssertApiHeaders(HttpResponseMessage response)
    {
        Assert.That(response.Headers.GetValues("X-Content-Type-Options"), Is.EqualTo(new[] { "nosniff" }));
        Assert.That(response.Headers.GetValues("X-Frame-Options"), Is.EqualTo(new[] { "DENY" }));
        Assert.That(response.Headers.GetValues("Referrer-Policy"), Is.EqualTo(new[] { "no-referrer" }));
        Assert.That(response.Headers.GetValues("Content-Security-Policy").Single(), Does.Contain("default-src 'none'"));
        Assert.That(response.Headers.CacheControl?.NoStore, Is.True);
        Assert.That(response.Headers.Contains("Server"), Is.False);
    }
}
