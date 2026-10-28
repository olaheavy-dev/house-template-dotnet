namespace Modules.Platform.Application.Notes;

/// <summary>The cache keys for notes, in one place, so whoever changes a note clears exactly what readers cached.</summary>
internal static class NoteCache
{
    public static string KeyFor(Guid id) => $"platform:note:{id}";
}
