namespace Api;

/// <summary>
/// Which process this image is: the API (the default) or a worker. Chosen at start with <c>--role=worker</c>
/// (or <c>ROLE=worker</c>), so one image serves both and a deploy scales them apart.
/// </summary>
internal static class AppRole
{
    public static bool IsWorker(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var role = configuration["role"];
        return role switch
        {
            null or "" => false,
            _ when role.Equals("api", StringComparison.OrdinalIgnoreCase) => false,
            _ when role.Equals("worker", StringComparison.OrdinalIgnoreCase) => true,
            _ => throw new InvalidOperationException($"Unknown role '{role}': use api or worker."),
        };
    }
}
