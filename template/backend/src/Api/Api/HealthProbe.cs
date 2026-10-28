namespace Api;

/// <summary>
/// <c>Api.dll healthcheck</c>: asks the API running in this container whether it is alive, for the image's
/// HEALTHCHECK (the chiseled image has no shell and no curl). Exit code 0 when <c>/health/live</c> answers 200.
/// </summary>
internal static class HealthProbe
{
    // One client for the process's one request: this runs before the host (and its client factory) exists.
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(3) };

    public static async Task<int> RunAsync(string? urls, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await Http.GetAsync(new Uri(AddressOf(urls), "health/live"), cancellationToken);
            return response.IsSuccessStatusCode ? 0 : 1;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return 1;
        }
    }

    // The first address ASPNETCORE_URLS lists, as this container reaches it: a wildcard host means "here".
    internal static Uri AddressOf(string? urls)
    {
        var first = (urls ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault()
            ?? "http://localhost:8080";
        var builder = new UriBuilder(first.Replace("://+", "://localhost", StringComparison.Ordinal).Replace("://*", "://localhost", StringComparison.Ordinal).Replace("://0.0.0.0", "://localhost", StringComparison.Ordinal));
        builder.Path = "/";
        return builder.Uri;
    }
}
