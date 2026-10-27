using System.Net;
using System.Text.Json;
using ErrorOr;
using Microsoft.AspNetCore.Mvc.Testing;

namespace IntegrationTests;

/// <summary>Every error is an RFC 9457 problem with a traceId: expected ones, unknown routes and bugs.</summary>
public sealed class ProblemDetailsTests
{
    private static readonly Uri Ping = new("/v1/platform/ping", UriKind.Relative);

    [Test]
    public async Task An_expected_error_is_a_problem_with_its_code_and_a_trace_id()
    {
        await using var api = PingHandlerStub.Api(() => Error.NotFound("platform.missing", "Nothing to ping."));
        using var client = api.CreateClient();

        using var response = await client.GetAsync(Ping);
        using var problem = await ProblemIn(response);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(problem.RootElement.GetProperty("code").GetString(), Is.EqualTo("platform.missing"));
        Assert.That(problem.RootElement.GetProperty("detail").GetString(), Is.EqualTo("Nothing to ping."));
        Assert.That(problem.RootElement.GetProperty("traceId").GetString(), Is.Not.Empty);
        Assert.That(problem.RootElement.GetProperty("instance").GetString(), Is.EqualTo("/v1/platform/ping"));
    }

    [Test]
    public async Task An_unhandled_exception_is_a_500_problem_that_hides_the_exception()
    {
        await using var api = PingHandlerStub.Api(() => throw new InvalidOperationException("secret connection string"));
        using var client = api.CreateClient();

        using var response = await client.GetAsync(Ping);
        var body = await response.Content.ReadAsStringAsync();
        using var problem = await ProblemIn(response);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
        Assert.That(problem.RootElement.GetProperty("code").GetString(), Is.EqualTo("internal.unexpected"));
        Assert.That(problem.RootElement.GetProperty("traceId").GetString(), Is.Not.Empty);
        Assert.That(body, Does.Not.Contain("secret").And.Not.Contain(nameof(InvalidOperationException)));
    }

    [Test]
    public async Task An_unknown_route_is_a_404_problem()
    {
        await using var api = new WebApplicationFactory<Program>();
        using var client = api.CreateClient();

        using var response = await client.GetAsync(new Uri("/v1/nowhere", UriKind.Relative));
        using var problem = await ProblemIn(response);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(problem.RootElement.GetProperty("status").GetInt32(), Is.EqualTo(404));
        Assert.That(problem.RootElement.GetProperty("traceId").GetString(), Is.Not.Empty);
    }

    private static async Task<JsonDocument> ProblemIn(HttpResponseMessage response)
    {
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"));
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }
}
