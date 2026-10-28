using System.Buffers.Text;
using System.Text.Json;
using ErrorOr;

namespace Common.Application.Paging;

/// <summary>
/// The opaque token that says where a page ended: the last row's sort key as URL-safe base64 of JSON.
/// Keyset, not offset: the next page starts after that key, so pages stay stable while rows are added
/// and deep pages cost no more than the first. Callers pass cursors back untouched; a damaged one is a 400.
/// </summary>
public static class Cursor
{
    public static string Encode<TKey>(TKey key) =>
        Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(key));

    /// <summary>The key a cursor holds: null for the first page (no cursor), an error if it isn't a cursor for <typeparamref name="TKey"/>.</summary>
    public static ErrorOr<TKey?> Decode<TKey>(string? cursor)
        where TKey : struct
    {
        if (cursor is null)
        {
            return default(TKey?);
        }

        try
        {
            var key = JsonSerializer.Deserialize<TKey?>(Base64Url.DecodeFromChars(cursor));
            return key is null ? Invalid : key;
        }
        catch (Exception exception) when (exception is FormatException or JsonException or ArgumentException)
        {
            return Invalid;
        }
    }

    private static Error Invalid { get; } = Error.Validation("cursor", "That page cursor isn't valid.");
}
