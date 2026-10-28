using System.ComponentModel.DataAnnotations;

namespace Modules.Platform.Application.Flags;

/// <summary>Settings under <c>Flags</c>.</summary>
public sealed class PublicFlagsOptions
{
    public const string Section = "Flags";

    /// <summary>
    /// The flags a signed-in user may be told about by <c>GET /v1/flags</c> (a front end reads them): a flag
    /// not listed here is never exposed, whatever its rules say.
    /// </summary>
    [MaxLength(100)]
    public IReadOnlyList<string> Public { get; init; } = [];
}
