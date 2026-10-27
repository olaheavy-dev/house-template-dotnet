using ErrorOr;
using Immediate.Handlers.Shared;

namespace Modules.Sample.Application.Status;

/// <summary>The module's first query: replace it with the module's own.</summary>
public sealed record GetSampleStatusQuery;

public sealed record SampleStatus(string Module, DateTimeOffset At);

[Handler]
internal sealed partial class GetSampleStatus(TimeProvider clock)
{
    private ValueTask<ErrorOr<SampleStatus>> HandleAsync(GetSampleStatusQuery query, CancellationToken cancellationToken) =>
        ValueTask.FromResult<ErrorOr<SampleStatus>>(new SampleStatus("Sample", clock.GetUtcNow()));
}
