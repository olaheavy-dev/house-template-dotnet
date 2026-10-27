using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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

    [When("I watch the API's pings")]
    public async Task WhenIWatchTheApisPings()
    {
        _response = await _client.GetAsync(new Uri("/v1/platform/pings", UriKind.Relative), HttpCompletionOption.ResponseHeadersRead);
        using var reader = new StreamReader(await _response.Content.ReadAsStreamAsync());
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (await reader.ReadLineAsync(timeout.Token) is { Length: > 0 } line)
        {
            if (line.StartsWith("data:", StringComparison.Ordinal))
            {
                _ping = JsonSerializer.Deserialize<Ping>(line["data:".Length..], JsonSerializerOptions.Web);
            }
        }
    }

    [Then("the first event is a ping that answers {string}")]
    public void ThenTheFirstEventIsAPingThatAnswers(string status) => ThenItAnswers(status);

    [Then("it names itself")]
    public void ThenItNamesItself() => Assert.That(_ping?.Service, Is.Not.Empty);

    public void Dispose()
    {
        _response?.Dispose();
        _client.Dispose();
    }

    private sealed record Ping(string Status, string Service, DateTimeOffset At);
}
