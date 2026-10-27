using System.Net;

namespace IntegrationTests;

/// <summary>The live document and Scalar are for development; clients use the build's backend/openapi/v1.json.</summary>
public sealed class OpenApiTests
{
    private static readonly Uri Document = new("/openapi/v1.json", UriKind.Relative);
    private static readonly Uri Reference = new("/scalar", UriKind.Relative);

    [Test]
    public async Task Development_serves_the_document_and_the_scalar_reference()
    {
        await using var api = new TestApi("Development");
        using var client = api.CreateClient();

        using var document = await client.GetAsync(Document);
        using var reference = await client.GetAsync(Reference);

        Assert.That(document.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(await document.Content.ReadAsStringAsync(), Does.Contain("/v1/platform/ping"));
        Assert.That(reference.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task Production_serves_neither()
    {
        await using var api = new TestApi("Production");
        using var client = api.CreateClient();

        using var document = await client.GetAsync(Document);
        using var reference = await client.GetAsync(Reference);

        Assert.That(document.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(reference.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }
}
