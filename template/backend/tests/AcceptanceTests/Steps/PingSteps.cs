using System.Net;
using System.Net.Http.Json;
using AcceptanceTests.Support;
using Reqnroll;

namespace AcceptanceTests.Steps;

[Binding]
public sealed class PingSteps : IDisposable
{
    private readonly HttpClient _client = ApiHost.CreateClient();
    private HttpResponseMessage? _response;
    private Ping? _ping;

    [When("I ping the API")]
    public async Task WhenIPingTheApi()
    {
        _response = await _client.GetAsync(new Uri("/v1/platform/ping", UriKind.Relative));
        _ping = await _response.Content.ReadFromJsonAsync<Ping>();
    }

    [Then("it answers {string}")]
    public void ThenItAnswers(string status)
    {
        Assert.That(_response?.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(_ping?.Status, Is.EqualTo(status));
    }

    [Then("it names itself")]
    public void ThenItNamesItself() => Assert.That(_ping?.Service, Is.Not.Empty);

    public void Dispose()
    {
        _response?.Dispose();
        _client.Dispose();
    }

    private sealed record Ping(string Status, string Service, DateTimeOffset At);
}
