using ErrorOr;
using Immediate.Handlers.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.Platform.Application.Ping;
using Modules.Platform.Infrastructure;

namespace Modules.Platform.UnitTests;

/// <summary>The handler through the module's own registration, with its pipeline steps.</summary>
public sealed class GetPingQueryHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task Handle_answers_ok_with_the_service_name_and_the_time()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Api:Title"] = "Demo App API" })
            .Build();
        await using var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<TimeProvider>(new FixedClock(Now))
            .AddPlatformModule(configuration)
            .BuildServiceProvider();
        var handler = services.GetRequiredService<IHandler<GetPingQuery, ErrorOr<PingResponse>>>();

        var result = await handler.HandleAsync(new GetPingQuery(), CancellationToken.None);

        Assert.That(result.IsError, Is.False);
        Assert.That(result.Value, Is.EqualTo(new PingResponse("ok", "Demo App API", Now)));
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
