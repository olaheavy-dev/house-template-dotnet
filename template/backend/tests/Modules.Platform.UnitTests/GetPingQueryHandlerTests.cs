using Modules.Platform.Application;
using Modules.Platform.Application.Ping;

namespace Modules.Platform.UnitTests;

public sealed class GetPingQueryHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task Handle_answers_ok_with_the_service_name_and_the_time()
    {
        var handler = new GetPingQueryHandler(new FixedClock(Now), new ServiceName("Demo App API"));

        var result = await handler.Handle(new GetPingQuery(), CancellationToken.None);

        Assert.That(result.IsError, Is.False);
        Assert.That(result.Value, Is.EqualTo(new PingResponse("ok", "Demo App API", Now)));
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
