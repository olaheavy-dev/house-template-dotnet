using Common.Application.Paging;
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
    // NUnit shares one fixture instance across tests, so each test gets its own store.
    private InMemoryNoteStore _store = null!;
    private ServiceProvider _services = null!;

    [SetUp]
    public void SetUp()
    {
        _store = new InMemoryNoteStore();
        var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton(TimeProvider.System)
            .AddPlatformModule(new ConfigurationBuilder().Build())
            .AddSingleton<INoteStore>(_store)
            .AddSingleton<INoteReader>(_store);
        services.AddHybridCache();
        _services = services.BuildServiceProvider();
    }

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

    [Test]
    public async Task List_walks_the_owners_notes_newest_first_one_page_at_a_time()
    {
        for (var i = 0; i < 5; i++)
        {
            await Create("alice", $"alice {i}");
        }

        await Create("bob", "bob 0");
        await Create("bob", "bob 1");

        var seen = new List<Guid>();
        string? cursor = null;
        var pages = 0;
        do
        {
            var page = (await List("alice", new PageRequest(2, cursor))).Value;
            seen.AddRange(page.Items.Select(note => note.Id));
            cursor = page.NextCursor;
            pages++;
        }
        while (cursor is not null);

        Assert.That(pages, Is.EqualTo(3));
        Assert.That(seen, Has.Count.EqualTo(5));
        Assert.That(seen, Is.Unique);
        Assert.That(seen, Is.Ordered.Descending);
        Assert.That(seen, Is.EquivalentTo(_store.Notes.Where(note => note.OwnerId == "alice").Select(note => note.Id)));
    }

    [Test]
    public async Task List_gives_the_last_page_no_cursor_when_it_ends_exactly_on_the_limit()
    {
        await Create("alice", "one");
        await Create("alice", "two");

        var page = (await List("alice", new PageRequest(2))).Value;

        Assert.That(page.Items, Has.Count.EqualTo(2));
        Assert.That(page.NextCursor, Is.Null);
    }

    [Test]
    public async Task List_refuses_a_damaged_cursor()
    {
        var result = await List("alice", new PageRequest(20, "not a cursor!"));

        Assert.That(result.FirstError.Type, Is.EqualTo(ErrorType.Validation));
        Assert.That(result.FirstError.Code, Is.EqualTo("cursor"));
    }

    [TestCase(0)]
    [TestCase(101)]
    public async Task List_refuses_a_limit_outside_1_to_100(int limit)
    {
        var result = await List("alice", new PageRequest(limit));

        Assert.That(result.IsError, Is.True);
        Assert.That(result.FirstError.Type, Is.EqualTo(ErrorType.Validation));
    }

    [Test]
    public async Task Get_reads_the_store_once_however_often_the_note_is_read()
    {
        var created = (await Create("alice", "buy milk")).Value;

        for (var i = 0; i < 5; i++)
        {
            Assert.That((await Get("alice", created.Id)).Value.Text, Is.EqualTo("buy milk"));
        }

        Assert.That(_store.FindCalls, Is.EqualTo(1));
    }

    [Test]
    public async Task Get_shares_one_trip_to_the_store_between_reads_that_arrive_together()
    {
        var created = (await Create("alice", "buy milk")).Value;
        _store.HoldFinds();

        var reads = Enumerable.Range(0, 10).Select(_ => Get("alice", created.Id).AsTask()).ToList();
        await _store.FindStarted;
        _store.ReleaseFinds();
        var results = await Task.WhenAll(reads);

        Assert.That(results.All(result => !result.IsError), Is.True);
        Assert.That(_store.FindCalls, Is.EqualTo(1));
    }

    [Test]
    public async Task Get_survives_the_request_that_started_the_shared_lookup_ending_first()
    {
        // Two requests miss the same key: the lookup runs once, on the first request's scope. That request
        // gives up and its scope is disposed while the lookup is still running; the second still needs the answer.
        var created = (await Create("alice", "buy milk")).Value;
        var gate = new TaskCompletionSource();
        var started = new TaskCompletionSource();
        var stores = new List<ScopedStore>();
        var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton(TimeProvider.System)
            .AddPlatformModule(new ConfigurationBuilder().Build())
            .AddScoped<INoteStore>(_ => new ScopedStore(_store, stores))
            .AddSingleton<INoteReader>(new GatedReader(_store, gate, started));
        services.AddHybridCache();
        await using var provider = services.BuildServiceProvider();
        var first = provider.CreateAsyncScope();
        var second = provider.CreateAsyncScope();
        using var giveUp = new CancellationTokenSource();

        var firstGet = first.ServiceProvider.GetRequiredService<IHandler<GetNoteQuery, ErrorOr<NoteResponse>>>()
            .HandleAsync(new GetNoteQuery("alice", created.Id), giveUp.Token).AsTask();
        await started.Task;
        var secondGet = second.ServiceProvider.GetRequiredService<IHandler<GetNoteQuery, ErrorOr<NoteResponse>>>()
            .HandleAsync(new GetNoteQuery("alice", created.Id), CancellationToken.None).AsTask();
        await giveUp.CancelAsync();
        try
        {
            await firstGet;
        }
        catch (OperationCanceledException)
        {
            // The caller that gave up sees its own cancellation.
        }

        await first.DisposeAsync();
        gate.SetResult();
        var result = await secondGet;
        await second.DisposeAsync();

        Assert.That(result.IsError, Is.False, "the request that stayed should still get its note");
        Assert.That(result.Value.Text, Is.EqualTo("buy milk"));
        Assert.That(stores, Is.Empty, "the lookup must not touch a store that belongs to a request");
    }

    [Test]
    public async Task Get_still_checks_the_owner_when_the_note_is_cached()
    {
        var created = (await Create("alice", "buy milk")).Value;
        await Get("alice", created.Id);

        var result = await Get("bob", created.Id);

        Assert.That(result.FirstError.Type, Is.EqualTo(ErrorType.NotFound));
    }

    [Test]
    public async Task Get_reports_a_missing_note_as_not_found()
    {
        var result = await Get("alice", Guid.CreateVersion7());

        Assert.That(result.FirstError.Type, Is.EqualTo(ErrorType.NotFound));
    }

    private ValueTask<ErrorOr<Page<NoteResponse>>> List(string owner, PageRequest page) =>
        _services.GetRequiredService<IHandler<ListNotesQuery, ErrorOr<Page<NoteResponse>>>>()
            .HandleAsync(new ListNotesQuery(owner, page), CancellationToken.None);

    private ValueTask<ErrorOr<NoteResponse>> Create(string owner, string text) =>
        _services.GetRequiredService<IHandler<CreateNoteCommand, ErrorOr<NoteResponse>>>()
            .HandleAsync(new CreateNoteCommand(owner, text), CancellationToken.None);

    private ValueTask<ErrorOr<NoteResponse>> Get(string owner, Guid id) =>
        _services.GetRequiredService<IHandler<GetNoteQuery, ErrorOr<NoteResponse>>>()
            .HandleAsync(new GetNoteQuery(owner, id), CancellationToken.None);

    /// <summary>A store tied to one request's scope: once that scope is disposed it can no longer be used.</summary>
    private sealed class ScopedStore(InMemoryNoteStore inner, List<ScopedStore> created) : INoteStore, IAsyncDisposable
    {
        private bool _disposed;

        public ValueTask DisposeAsync()
        {
            _disposed = true;
            return ValueTask.CompletedTask;
        }

        public Task AddAsync(Note note, CancellationToken cancellationToken)
        {
            Touch();
            return inner.AddAsync(note, cancellationToken);
        }

        public Task<IReadOnlyList<Note>> ListNewestAsync(string ownerId, Guid? before, int take, CancellationToken cancellationToken)
        {
            Touch();
            return inner.ListNewestAsync(ownerId, before, take, cancellationToken);
        }

        public ValueTask PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken)
            where TEvent : class
        {
            Touch();
            return inner.PublishAsync(integrationEvent, cancellationToken);
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            Touch();
            return inner.SaveChangesAsync(cancellationToken);
        }

        private void Touch()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            created.Add(this);
        }
    }

    /// <summary>The singleton reader the cache uses: it holds the lookup until released, as a slow database would.</summary>
    private sealed class GatedReader(InMemoryNoteStore inner, TaskCompletionSource gate, TaskCompletionSource started) : INoteReader
    {
        public async Task<Note?> FindAsync(Guid id, CancellationToken cancellationToken)
        {
            started.TrySetResult();
            await gate.Task;
            return await inner.FindAsync(id, cancellationToken);
        }
    }

    private sealed class InMemoryNoteStore : INoteStore, INoteReader
    {
        public List<Note> Notes { get; } = [];

        private readonly TaskCompletionSource _findStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private TaskCompletionSource? _hold;
        private int _findCalls;

        public List<object> Published { get; } = [];

        public int Saved { get; private set; }

        public Task AddAsync(Note note, CancellationToken cancellationToken)
        {
            Notes.Add(note);
            return Task.CompletedTask;
        }

        public int FindCalls => _findCalls;

        public Task FindStarted => _findStarted.Task;

        public void HoldFinds() => _hold = new TaskCompletionSource();

        public void ReleaseFinds() => _hold?.SetResult();

        public async Task<Note?> FindAsync(Guid id, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _findCalls);
            _findStarted.TrySetResult();
            if (_hold is { } hold)
            {
                await hold.Task;
            }

            return Notes.FirstOrDefault(note => note.Id == id);
        }

        public ValueTask PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken)
            where TEvent : class
        {
            Published.Add(integrationEvent);
            return ValueTask.CompletedTask;
        }

        public Task<IReadOnlyList<Note>> ListNewestAsync(string ownerId, Guid? before, int take, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Note>>([.. Notes
                .Where(note => note.OwnerId == ownerId && (before is null || note.Id.CompareTo(before.Value) < 0))
                .OrderByDescending(note => note.Id)
                .Take(take)]);

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            Saved++;
            return Task.CompletedTask;
        }
    }
}
