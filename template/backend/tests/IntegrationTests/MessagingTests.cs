using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Modules.Platform.Application.Notes;
using Modules.Platform.Domain;
using Modules.Platform.IntegrationEvents;
using Wolverine;
using Wolverine.Runtime;
using Wolverine.Runtime.Routing;

namespace IntegrationTests;

/// <summary>Integration events: stored with the data that caused them, delivered after it commits, never otherwise.</summary>
public sealed class MessagingTests
{
    private static readonly Uri Notes = new("/v1/platform/notes", UriKind.Relative);

    [Test]
    public async Task A_created_note_reaches_its_subscriber()
    {
        await using var api = new TestApi(services => services.AddLogging(logging => logging.AddFakeLogging()));
        using var client = api.CreateClient();
        client.DefaultRequestHeaders.Add("X-Dev-User", "alice");

        using var response = await client.PostAsJsonAsync(Notes, new { text = "buy milk" });
        var note = await response.Content.ReadFromJsonAsync<NoteBody>();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        Assert.That(await Delivered(api, note!.Id), Is.True, "the subscriber never saw the event");
    }

    [Test]
    public async Task Nothing_is_delivered_when_the_save_fails()
    {
        await using var api = new TestApi(services => services.AddLogging(logging => logging.AddFakeLogging()));
        var noteId = Guid.CreateVersion7();

        await using (var scope = api.Services.CreateAsyncScope())
        {
            var notes = scope.ServiceProvider.GetRequiredService<INoteStore>();
            // An owner id over the column's 200 characters: the save is refused after the event was queued.
            await notes.AddAsync(Note.Create(noteId, new string('x', 201), "buy milk").Value, CancellationToken.None);
            await notes.PublishAsync(new NoteCreated(noteId, "alice", DateTimeOffset.UnixEpoch), CancellationToken.None);

            await Assert.ThrowsAsync<DbUpdateException>(() => notes.SaveChangesAsync(CancellationToken.None));
        }

        Assert.That(await Delivered(api, noteId, TimeSpan.FromSeconds(3)), Is.False, "an event for a note that was never stored was delivered");
    }

    [Test]
    public async Task The_same_event_delivered_twice_is_handled_once()
    {
        await using var api = new TestApi(services => services.AddLogging(logging => logging.AddFakeLogging()));
        var runtime = api.Services.GetRequiredService<IWolverineRuntime>();
        var noteId = Guid.CreateVersion7();
        var message = new NoteCreated(noteId, "alice", DateTimeOffset.UnixEpoch);

        var router = runtime.RoutingFor(typeof(NoteCreated));
        var sender = router.Routes.OfType<MessageRoute>().Single().Sender;
        var first = router.RouteForPublish(message, new DeliveryOptions()).Single();
        await sender.StoreAndForwardAsync(first);
        Assert.That(await Delivered(api, noteId), Is.True, "the subscriber never saw the event");

        // The same envelope id again, as a retry or a redelivery after a lost acknowledgement would send it. Storing it
        // is what the inbox answers: it refuses a message id it already holds (Wolverine retries the store a few times
        // and logs warnings before it decides the envelope is a duplicate).
        var again = router.RouteForPublish(message, new DeliveryOptions()).Single();
        again.Id = first.Id;
        await sender.StoreAndForwardAsync(again);
        await Task.Delay(TimeSpan.FromSeconds(3));

        Assert.That(Handled(api, noteId), Is.EqualTo(1), "the inbox should drop an envelope it has already handled");
    }

    private static async Task<bool> Delivered(TestApi api, Guid noteId, TimeSpan? within = null)
    {
        var collector = api.Services.GetFakeLogCollector();
        var deadline = DateTimeOffset.UtcNow + (within ?? TimeSpan.FromSeconds(15));
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (collector.GetSnapshot().Any(record => record.Level == LogLevel.Information && record.Message.Contains($"A note was created: {noteId}", StringComparison.Ordinal)))
            {
                return true;
            }

            await Task.Delay(100);
        }

        return false;
    }

    private static int Handled(TestApi api, Guid noteId) =>
        api.Services.GetFakeLogCollector().GetSnapshot().Count(record =>
            record.Level == LogLevel.Information && record.Message.Contains($"A note was created: {noteId}", StringComparison.Ordinal));

    private sealed record NoteBody(Guid Id);
}
