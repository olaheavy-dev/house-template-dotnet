using ErrorOr;
using Immediate.Handlers.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.Sample.Application.Status;
using Modules.Sample.Infrastructure;

namespace Modules.Sample.UnitTests;

/// <summary>The handler through the module's own registration, with its pipeline steps.</summary>
public sealed class GetSampleStatusTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task Handle_answers_the_module_and_the_time()
    {
        await using var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<TimeProvider>(new FixedClock(Now))
            .AddSampleModule(new ConfigurationBuilder().Build())
            .BuildServiceProvider();
        var handler = services.GetRequiredService<IHandler<GetSampleStatusQuery, ErrorOr<SampleStatus>>>();

        var result = await handler.HandleAsync(new GetSampleStatusQuery(), CancellationToken.None);

        Assert.That(result.Value, Is.EqualTo(new SampleStatus("Sample", Now)));
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
