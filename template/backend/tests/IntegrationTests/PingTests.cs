using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace IntegrationTests;

/// <summary>
/// Technical checks on the whole host in memory: the wire format and HTTP details. Behaviour, in the
/// product's words, lives in AcceptanceTests' feature files.
/// </summary>
public sealed class PingTests
{
    private WebApplicationFactory<Program> _factory = null!;

    [OneTimeSetUp]
    public void StartTheApi() => _factory = new WebApplicationFactory<Program>();

    [OneTimeTearDown]
    public async Task StopTheApi() => await _factory.DisposeAsync();

    [Test]
    public async Task Ping_answers_json_with_camel_case_fields()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/v1/platform/ping", UriKind.Relative));
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/json"));
        Assert.That(
            body.RootElement.EnumerateObject().Select(property => property.Name),
            Is.EquivalentTo(new[] { "status", "service", "at" }));
    }
}
