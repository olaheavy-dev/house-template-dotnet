using System.Net;
using System.Text.Json;
using Common.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Modules.Platform.Domain;
using Modules.Platform.Infrastructure.Persistence;
using Npgsql;
using Testcontainers.PostgreSql;

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
    public async Task The_modules_tables_and_migration_history_live_in_its_own_schema()
    {
        await using var connection = new NpgsqlConnection(TestDatabase.ConnectionString);
        await connection.OpenAsync();

        Assert.That(await SchemasHolding("notes", connection), Is.EqualTo(new[] { "platform" }));
        // Every module has a history table in its own schema; none lands in public.
        var histories = await SchemasHolding("__ef_migrations", connection);
        Assert.That(histories, Does.Contain("platform"));
        Assert.That(histories, Does.Not.Contain("public"));
    }

    [Test]
    public async Task Ready_is_503_naming_the_database_once_it_goes_away()
    {
        // The API needs its database to start (messaging registers the node there), so take it away after.
        await using var postgres = new PostgreSqlBuilder(TestDatabase.Image).Build();
        await postgres.StartAsync();
        await using var api = new TestApi(
            _ => { },
            settings: new Dictionary<string, string> { ["ConnectionStrings:Default"] = postgres.GetConnectionString() });
        using var client = api.CreateClient();
        using (var before = await client.GetAsync(new Uri("/health/ready", UriKind.Relative)))
        {
            Assert.That(before.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        await postgres.StopAsync();
        using var response = await client.GetAsync(new Uri("/health/ready", UriKind.Relative));
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
        var failing = body.RootElement.GetProperty("failing").EnumerateArray().Select(name => name.GetString()).ToList();
        Assert.That(failing, Does.Contain("database:platform"));
        Assert.That(failing, Is.All.StartWith("database:"));
    }

    private static async Task<List<string>> SchemasHolding(string table, NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand(
            "select table_schema from information_schema.tables where table_name = @table order by 1", connection);
        command.Parameters.AddWithValue("table", table);
        await using var reader = await command.ExecuteReaderAsync();
        var schemas = new List<string>();
        while (await reader.ReadAsync())
        {
            schemas.Add(reader.GetString(0));
        }

        return schemas;
    }

    private PlatformDbContext NewContext() =>
        new(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseNpgsql(TestDatabase.ConnectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations", PlatformDbContext.Schema))
            .AddInterceptors(new AuditInterceptor(_clock))
            .Options);
}
