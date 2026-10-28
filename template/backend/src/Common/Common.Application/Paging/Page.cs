namespace Common.Application.Paging;

/// <summary>One page of a list, and the cursor of the next one (null on the last page).</summary>
public sealed record Page<T>(IReadOnlyList<T> Items, string? NextCursor);

public static class Page
{
    /// <summary>
    /// A page from the <c>limit + 1</c> rows a keyset query fetched: the extra row only says there is a
    /// next page, and the last shown row's key becomes its cursor.
    /// </summary>
    public static Page<TItem> From<TRow, TKey, TItem>(
        IReadOnlyList<TRow> rows, PageRequest request, Func<TRow, TKey> key, Func<TRow, TItem> item)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(item);
        var shown = rows.Take(request.Limit).ToList();
        var more = rows.Count > request.Limit;
        return new Page<TItem>([.. shown.Select(item)], more && shown.Count > 0 ? Cursor.Encode(key(shown[^1])) : null);
    }
}
