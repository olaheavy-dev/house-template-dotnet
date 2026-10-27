namespace IntegrationTests;

/// <summary>The backend/ directory, found by walking up from the test binaries to Backend.slnx.</summary>
internal static class Backend
{
    public static string Directory { get; } = Find();

    private static string Find()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Backend.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("Backend.slnx not found above " + AppContext.BaseDirectory);
    }
}
