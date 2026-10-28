using ErrorOr;
using Immediate.Handlers.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.Platform.Application.Notes;
using Modules.Platform.Domain;
using Modules.Platform.Infrastructure;
using Modules.Platform.IntegrationEvents;

namespace Modules.Platform.UnitTests;

/// <summary>The note handlers through the module's registration, with the store in memory.</summary>
public sealed class NoteHandlerTests
{
    private readonly InMemoryNoteStore _store = new();
    private ServiceProvider _services = null!;

    [SetUp]
    public void SetUp() =>
        _services = new ServiceCollection()
            .AddLogging()
            .AddSingleton(TimeProvider.System)
            .AddPlatformModule(new ConfigurationBuilder().Build())
            .AddSingleton<INoteStore>(_store)
            .BuildServiceProvider();

    [TearDown]
    public async Task TearDown() => await _services.DisposeAsync();

    [Test]
    public async Task Create_stores_the_note_for_its_owner()
    {
        var result = await Create("alice", "buy milk");

        Assert.That(result.IsError, Is.False);
        Assert.That(_store.Saved, Is.EqualTo(1));
        Assert.That(_store.Notes.Single().OwnerId, Is.EqualTo("alice"));
        var published = (NoteCreated)_store.Published.Single();
        Assert.That(published.NoteId, Is.EqualTo(result.Value.Id));
        Assert.That(published.OwnerId, Is.EqualTo("alice"));
    }

    [Test]
    public async Task Create_stores_nothing_for_a_blank_text()
    {
        var result = await Create("alice", " ");

        Assert.That(result.IsError, Is.True);
        Assert.That(_store.Saved, Is.Zero);
        Assert.That(_store.Published, Is.Empty);
    }

    [Test]
    public async Task Create_refuses_a_missing_text()
    {
        var result = await Create("alice", null!);

        Assert.That(result.IsError, Is.True);
        Assert.That(_store.Saved, Is.Zero);
    }

    [Test]
    public async Task Get_returns_the_owners_note()
    {
        var created = (await Create("alice", "buy milk")).Value;

        var result = await Get("alice", created.Id);

        Assert.That(result.Value.Text, Is.EqualTo("buy milk"));
    }

    [Test]
    public async Task Get_reports_someone_elses_note_as_missing()
    {
        var created = (await Create("alice", "buy milk")).Value;

        var result = await Get("bob", created.Id);

        Assert.That(result.FirstError.Type, Is.EqualTo(ErrorType.NotFound));
    }

    private ValueTask<ErrorOr<NoteResponse>> Create(string owner, string text) =>
        _services.GetRequiredService<IHandler<CreateNoteCommand, ErrorOr<NoteResponse>>>()
            .HandleAsync(new CreateNoteCommand(owner, text), CancellationToken.None);

    private ValueTask<ErrorOr<NoteResponse>> Get(string owner, Guid id) =>
        _services.GetRequiredService<IHandler<GetNoteQuery, ErrorOr<NoteResponse>>>()
            .HandleAsync(new GetNoteQuery(owner, id), CancellationToken.None);

    private sealed class InMemoryNoteStore : INoteStore
    {
        public List<Note> Notes { get; } = [];

        public List<object> Published { get; } = [];

        public int Saved { get; private set; }

        public Task AddAsync(Note note, CancellationToken cancellationToken)
        {
            Notes.Add(note);
            return Task.CompletedTask;
        }

        public Task<Note?> FindAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Notes.FirstOrDefault(note => note.Id == id));

        public ValueTask PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken)
            where TEvent : class
        {
            Published.Add(integrationEvent);
            return ValueTask.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            Saved++;
            return Task.CompletedTask;
        }
    }
}
