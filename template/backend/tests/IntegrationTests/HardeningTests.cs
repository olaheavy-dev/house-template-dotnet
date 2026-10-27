using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Api;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Options;

namespace IntegrationTests;

/// <summary>The host's limits: body size, timeouts, the rate limit, CORS, allowed hosts and its settings.</summary>
public sealed class HardeningTests
{
    private static readonly Uri Ping = new("/v1/platform/ping", UriKind.Relative);

    [Test]
    public async Task A_body_over_the_limit_is_a_413_problem()
    {
        // Kestrel enforces the limit; the in-memory test server doesn't, so this runs on a real port.
        await using var api = new TestApi(TestEndpoints.AddTo, settings: new Dictionary<string, string> { ["Api:MaxRequestBodyBytes"] = "100" });
        api.UseKestrel(0);
        api.StartServer();
        using var client = api.CreateClient();
        using var body = Json("{\"text\":\"" + new string('x', 1_000) + "\"}");

        using var response = await client.PostAsync(new Uri("/test/echo", UriKind.Relative), body);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.RequestEntityTooLarge));
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"));
    }

    [Test]
    public async Task A_body_within_the_limit_is_read()
    {
        await using var api = new TestApi(TestEndpoints.AddTo, settings: new Dictionary<string, string> { ["Api:MaxRequestBodyBytes"] = "100" });
        api.UseKestrel(0);
        api.StartServer();
        using var client = api.CreateClient();
        using var body = Json("{\"text\":\"short\"}");

        using var response = await client.PostAsync(new Uri("/test/echo", UriKind.Relative), body);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task A_request_over_the_timeout_is_a_504_problem()
    {
        await using var api = new TestApi(TestEndpoints.AddTo, settings: new Dictionary<string, string> { ["Api:RequestTimeoutSeconds"] = "1" });
        using var client = api.CreateClient();

        using var response = await client.GetAsync(new Uri("/test/slow", UriKind.Relative));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.GatewayTimeout));
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"));
    }

    [Test]
    public async Task A_caller_over_the_rate_limit_gets_a_429_problem_with_retry_after()
    {
        await using var api = new TestApi(_ => { }, settings: new Dictionary<string, string> { ["Api:RateLimitPerMinute"] = "2" });
        using var client = api.CreateClient();

        using var first = await client.GetAsync(Ping);
        using var second = await client.GetAsync(Ping);
        using var third = await client.GetAsync(Ping);
        using var problem = JsonDocument.Parse(await third.Content.ReadAsStringAsync());

        Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(third.StatusCode, Is.EqualTo(HttpStatusCode.TooManyRequests));
        Assert.That(problem.RootElement.GetProperty("code").GetString(), Is.EqualTo("request.rate_limited"));
        Assert.That(int.Parse(third.Headers.GetValues("Retry-After").Single(), System.Globalization.CultureInfo.InvariantCulture), Is.InRange(1, 60));
    }

    [Test]
    public async Task Health_probes_are_never_rate_limited()
    {
        await using var api = new TestApi(_ => { }, settings: new Dictionary<string, string> { ["Api:RateLimitPerMinute"] = "1" });
        using var client = api.CreateClient();

        for (var probe = 0; probe < 5; probe++)
        {
            using var response = await client.GetAsync(new Uri("/health/live", UriKind.Relative));
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }
    }

    [Test]
    public async Task A_configured_origin_may_call_the_api_from_a_browser()
    {
        await using var api = new TestApi(_ => { }, settings: new Dictionary<string, string> { ["Api:CorsOrigins:0"] = "https://app.example.com" });
        using var client = api.CreateClient();

        using var preflightRequest = Preflight("https://app.example.com");
        using var preflight = await client.SendAsync(preflightRequest);
        using var request = new HttpRequestMessage(HttpMethod.Get, Ping);
        request.Headers.Add("Origin", "https://app.example.com");
        using var response = await client.SendAsync(request);

        Assert.That(preflight.Headers.GetValues("Access-Control-Allow-Origin"), Is.EqualTo(new[] { "https://app.example.com" }));
        Assert.That(response.Headers.GetValues("Access-Control-Expose-Headers").Single(), Does.Contain("X-Request-ID"));
    }

    [Test]
    public async Task Any_other_origin_gets_no_cors_headers()
    {
        await using var api = new TestApi(_ => { }, settings: new Dictionary<string, string> { ["Api:CorsOrigins:0"] = "https://app.example.com" });
        using var client = api.CreateClient();

        using var preflightRequest = Preflight("https://evil.example.com");
        using var preflight = await client.SendAsync(preflightRequest);

        Assert.That(preflight.Headers.Contains("Access-Control-Allow-Origin"), Is.False);
    }

    [Test]
    public async Task A_500_problem_still_carries_the_cors_headers()
    {
        // Without them a browser reports a CORS error instead of the problem.
        await using var api = new TestApi(
            services => services.AddPingHandlerStub(() => throw new InvalidOperationException("boom")),
            settings: new Dictionary<string, string> { ["Api:CorsOrigins:0"] = "https://app.example.com" });
        using var client = api.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, Ping);
        request.Headers.Add("Origin", "https://app.example.com");

        using var response = await client.SendAsync(request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
        Assert.That(response.Headers.GetValues("Access-Control-Allow-Origin"), Is.EqualTo(new[] { "https://app.example.com" }));
    }

    [Test]
    public async Task A_host_not_allowed_is_refused()
    {
        await using var api = new TestApi(_ => { }, settings: new Dictionary<string, string> { ["AllowedHosts"] = "api.example.com" });
        using var refused = api.CreateClient();
        using var allowed = api.CreateClient(new() { BaseAddress = new Uri("http://api.example.com") });

        using var wrongHost = await refused.GetAsync(Ping);
        using var rightHost = await allowed.GetAsync(Ping);

        Assert.That(wrongHost.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(rightHost.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [TestCase("Api:MaxRequestBodyBytes", "0")]
    [TestCase("Api:RequestTimeoutSeconds", "0")]
    [TestCase("Api:RateLimitPerMinute", "0")]
    [TestCase("Api:CorsOrigins:0", "https://app.example.com/")]
    [TestCase("Api:CorsOrigins:0", "*")]
    [TestCase("Api:CorsOrigins:0", "app.example.com")]
    [TestCase("Auth:Issuer", "keycloak/realms/app")]
    [TestCase("Auth:MetadataAddress", "ftp://keycloak/realms/app")]
    [TestCase("Auth:Audience", "")]
    public void A_bad_setting_stops_the_app_from_starting(string key, string value) =>
        // The checks the host runs when it starts. (Starting a whole WebApplicationFactory host on a
        // bad setting sometimes reports its own disposal instead of the validation error.)
        Assert.That(() => ValidateAtStartWith(key, value), Throws.InstanceOf<OptionsValidationException>());

    [TestCase("Api:CorsOrigins:0", "https://app.example.com")]
    [TestCase("Api:CorsOrigins:0", "http://localhost:3000")]
    [TestCase("Auth:Issuer", "http://localhost:8080/realms/app")]
    [TestCase("Api:RateLimitPerMinute", "1")]
    public void A_good_setting_starts(string key, string value) =>
        Assert.That(() => ValidateAtStartWith(key, value), Throws.Nothing);

    private static void ValidateAtStartWith(string key, string value)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [key] = value })
            .Build();
        var services = new ServiceCollection();
        services.AddHardening(configuration);
        services.AddAuth(configuration, new HostingEnvironment { EnvironmentName = Environments.Production });
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IStartupValidator>().Validate();
    }

    private static StringContent Json(string json) => new(json, Encoding.UTF8, new MediaTypeHeaderValue("application/json"));

    private static HttpRequestMessage Preflight(string origin) => new(HttpMethod.Options, Ping)
    {
        Headers =
        {
            { "Origin", origin },
            { "Access-Control-Request-Method", "GET" },
        },
    };
}
