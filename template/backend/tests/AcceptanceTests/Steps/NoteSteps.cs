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
    private readonly List<List<Note>> _pages = [];
    private readonly List<Guid> _saved = [];

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
        if (_note is not null)
        {
            _saved.Add(_note.Id);
        }
    }

    [When("I list my notes {int} at a time")]
    public async Task WhenIListMyNotesAtATime(int limit)
    {
        string? cursor = null;
        do
        {
            _response?.Dispose();
            var query = $"/v1/platform/notes?limit={limit}" + (cursor is null ? string.Empty : $"&cursor={Uri.EscapeDataString(cursor)}");
            _response = await _client.GetAsync(new Uri(query, UriKind.Relative));
            if (!_response.IsSuccessStatusCode)
            {
                return;
            }

            var page = (await _response.Content.ReadFromJsonAsync<NotePage>())!;
            _pages.Add(page.Items);
            cursor = page.NextCursor;
        }
        while (cursor is not null);
    }

    [When("I list my notes starting from the cursor {string}")]
    public async Task WhenIListMyNotesStartingFromTheCursor(string cursor)
    {
        _response?.Dispose();
        _response = await _client.GetAsync(new Uri($"/v1/platform/notes?cursor={Uri.EscapeDataString(cursor)}", UriKind.Relative));
    }

    [Then("I get pages of 2, 2 and 1 notes")]
    public void ThenIGetPagesOf221Notes() => Assert.That(_pages.Select(page => page.Count), Is.EqualTo(new[] { 2, 2, 1 }));

    [Then("every one of my notes appears once")]
    public void ThenEveryOneOfMyNotesAppearsOnce() =>
        Assert.That(_pages.SelectMany(page => page).Select(note => note.Id), Is.EquivalentTo(_saved));

    [Then("the request is refused as invalid")]
    public void ThenTheRequestIsRefusedAsInvalid() => Assert.That(_response?.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

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

    private sealed record NotePage(List<Note> Items, string? NextCursor);
}
