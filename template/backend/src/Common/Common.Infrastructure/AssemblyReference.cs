using System.Reflection;

namespace Common.Infrastructure;

/// <summary>Names this assembly for registration and the architecture tests.</summary>
public static class AssemblyReference
{
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
}
