using System.Net;
using System.Text.Json;
using Common.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Modules.Platform.Domain;
using Modules.Platform.Infrastructure.Persistence;
using Npgsql;

namespace IntegrationTests;

/// <summary>What the persistence plumbing promises, against the real database.</summary>
public sealed class PersistenceTests
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 28, 9, 0, 0, TimeSpan.Zero));

    [Test]
    public async Task A_save_stamps_created_and_updated_from_the_clock()
    {
        var id = Guid.CreateVersion7();
        await using (var db = NewContext())
        {
            db.Notes.Add(Note.Create(id, "alice", "one").Value);
            await db.SaveChangesAsync();
        }

        _clock.Advance(TimeSpan.FromMinutes(5));
        await using (var db = NewContext())
        {
            (await db.Notes.SingleAsync(note => note.Id == id)).Edit("two");
            await db.SaveChangesAsync();
        }

        await using var check = NewContext();
        var stored = await check.Notes.SingleAsync(note => note.Id == id);
        Assert.That(stored.CreatedAt, Is.EqualTo(new DateTimeOffset(2026, 10, 28, 9, 0, 0, TimeSpan.Zero)));
        Assert.That(stored.UpdatedAt, Is.EqualTo(new DateTimeOffset(2026, 10, 28, 9, 5, 0, TimeSpan.Zero)));
    }

    [Test]
    public async Task A_stale_save_is_refused()
    {
        var id = Guid.CreateVersion7();
        await using (var db = NewContext())
        {
            db.Notes.Add(Note.Create(id, "alice", "one").Value);
            await db.SaveChangesAsync();
        }

        await using var first = NewContext();
        await using var second = NewContext();
        (await first.Notes.SingleAsync(note => note.Id == id)).Edit("first");
        (await second.Notes.SingleAsync(note => note.Id == id)).Edit("second");
        await first.SaveChangesAsync();

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }

    [Test]
    public async Task The_modules_tables_live_in_its_own_schema()
    {
        await using var connection = new NpgsqlConnection(TestDatabase.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "select table_schema from information_schema.tables where table_name in ('notes', '__ef_migrations') order by 1", connection);
        await using var reader = await command.ExecuteReaderAsync();
        var schemas = new List<string>();
        while (await reader.ReadAsync())
        {
            schemas.Add(reader.GetString(0));
        }

        Assert.That(schemas, Is.EqualTo(new[] { "platform", "platform" }));
    }

    [Test]
    public async Task Ready_is_503_naming_the_database_when_it_cannot_be_reached()
    {
        await using var api = new TestApi(
            _ => { },
            settings: new Dictionary<string, string> { ["ConnectionStrings:Default"] = "Host=localhost;Port=1;Timeout=1;Username=x;Password=x" });
        using var client = api.CreateClient();

        using var response = await client.GetAsync(new Uri("/health/ready", UriKind.Relative));
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
        Assert.That(
            body.RootElement.GetProperty("failing").EnumerateArray().Select(name => name.GetString()),
            Is.EqualTo(new[] { "database:platform" }));
    }

    private PlatformDbContext NewContext() =>
        new(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseNpgsql(TestDatabase.ConnectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations", PlatformDbContext.Schema))
            .AddInterceptors(new AuditInterceptor(_clock))
            .Options);
}
