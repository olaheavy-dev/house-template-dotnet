using System.Net;
using System.Net.Http.Json;
using AcceptanceTests.Support;
using Reqnroll;

namespace AcceptanceTests.Steps;

[Binding]
public sealed class NoteSteps : IDisposable
{
    private readonly HttpClient _client = ApiHost.CreateClient();
    private HttpResponseMessage? _response;
    private Note? _note;
    private Uri? _location;

    [Given("I am signed in as {string}")]
    [When("I am signed in as {string}")]
    public void GivenIAmSignedInAs(string user)
    {
        // In Development the X-Dev-User header stands in for a real sign-in.
        _client.DefaultRequestHeaders.Remove("X-Dev-User");
        _client.DefaultRequestHeaders.Add("X-Dev-User", user);
    }

    [Given("I saved the note {string}")]
    [When("I save the note {string}")]
    public async Task WhenISaveTheNote(string text)
    {
        _response?.Dispose();
        _response = await _client.PostAsJsonAsync(new Uri("/v1/platform/notes", UriKind.Relative), new { text });
        _location = _response.Headers.Location;
        _note = _response.IsSuccessStatusCode ? await _response.Content.ReadFromJsonAsync<Note>() : null;
    }

    [Then("the note is created")]
    public void ThenTheNoteIsCreated() => Assert.That(_response?.StatusCode, Is.EqualTo(HttpStatusCode.Created));

    [Then("I can read it back as {string}")]
    public async Task ThenICanReadItBackAs(string text)
    {
        using var response = await _client.GetAsync(_location);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await response.Content.ReadFromJsonAsync<Note>())?.Text, Is.EqualTo(text));
    }

    [Then("the note is refused as invalid")]
    public void ThenTheNoteIsRefusedAsInvalid() => Assert.That(_response?.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

    [Then("I cannot read alice's note")]
    public async Task ThenICannotReadAlicesNote()
    {
        using var response = await _client.GetAsync(new Uri($"/v1/platform/notes/{_note?.Id}", UriKind.Relative));
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Then("I am told to sign in")]
    public void ThenIAmToldToSignIn() => Assert.That(_response?.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

    public void Dispose()
    {
        _response?.Dispose();
        _client.Dispose();
    }

    private sealed record Note(Guid Id, string Text);
}
