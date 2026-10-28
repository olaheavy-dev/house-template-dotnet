using Common.Application.Paging;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Mvc;

namespace Common.Presentation;

/// <summary>
/// The paging query string of a list endpoint: <c>?limit=20&amp;cursor=...</c>. Bind it with
/// <c>[AsParameters] PageQuery page</c> and pass <see cref="ToRequest"/> to the query.
/// </summary>
/// <param name="Limit">How many items, 1 to 100 (20 when not given).</param>
/// <param name="Cursor">The <c>nextCursor</c> of the previous page, untouched.</param>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)] // bound from the query string
public sealed record PageQuery(
    [FromQuery(Name = "limit")] int? Limit = null,
    [FromQuery(Name = "cursor")] string? Cursor = null)
{
    public PageRequest ToRequest() => new(Limit ?? PageRequest.DefaultLimit, Cursor);
}
