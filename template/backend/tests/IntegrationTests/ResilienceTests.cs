using System.Net;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests;

/// <summary>Every HttpClient from the factory retries a transient failure.</summary>
public sealed class ResilienceTests
{
    [Test]
    public async Task A_client_from_the_factory_retries_a_503()
    {
        var calls = new Calls();
        await using var api = new TestApi(services => services.AddHttpClient("upstream")
            .ConfigurePrimaryHttpMessageHandler(() => new FlakyUpstream(failures: 1, calls)));
        using var client = api.Services.GetRequiredService<IHttpClientFactory>().CreateClient("upstream");

        using var response = await client.GetAsync(new Uri("http://upstream.test/"));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(calls.Count, Is.EqualTo(2));
    }

    private sealed class Calls
    {
        public int Count { get; set; }
    }

    /// <summary>Answers 503 to the first <paramref name="failures"/> calls, then 200.</summary>
    private sealed class FlakyUpstream(int failures, Calls calls) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            calls.Count++;
            return Task.FromResult(new HttpResponseMessage(calls.Count <= failures ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK));
        }
    }
}
