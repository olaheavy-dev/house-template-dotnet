using System.Reflection;
using System.Text.RegularExpressions;
using ArchUnitNET.Domain;
using ArchUnitNET.Domain.Dependencies;
using Assembly = System.Reflection.Assembly;

namespace ArchitectureTests;

/// <summary>The code a rule checks: ArchUnitNET's model of the compiled assemblies, and the assemblies.</summary>
public sealed record CheckedCode(Architecture Architecture, IReadOnlyList<Assembly> Assemblies);

/// <summary>An architecture rule: its name, why it exists, and what in the code breaks it.</summary>
public sealed record ArchitectureRule(string Name, string Why, Func<CheckedCode, IReadOnlyList<string>> Violations)
{
    public override string ToString() => Name;
}

/// <summary>
/// The rules every module follows. Each one runs against the real code (AppRuleTests) and against
/// ArchitectureTests.Fixtures, code that breaks it on purpose (RuleSelfTests): a rule that can't fail
/// checks nothing. ArchUnitNET reads the compiled IL, so the tests run in Debug too: Release
/// optimisations can hide a dependency.
/// </summary>
public static partial class HouseRules
{
    public static IReadOnlyList<ArchitectureRule> All { get; } =
    [
        new(
            "DomainDependsOnNoOuterLayer",
            "The domain holds the business rules; it can't know about use cases, storage, HTTP or EF Core.",
            code => Dependencies(code, Layer("Domain"), target =>
                OuterLayer().IsMatch(target) || Framework().IsMatch(target))),
        new(
            "ApplicationDependsOnNoOuterLayer",
            "Use cases can't depend on how they're stored or served.",
            code => Dependencies(code, Layer("Application"), target =>
                InfrastructureOrPresentation().IsMatch(target) || AspNetCoreOrEf().IsMatch(target))),
        new(
            "PresentationDependsOnNoInfrastructure",
            "Endpoints go through handlers, never around them to storage.",
            code => Dependencies(code, Layer("Presentation"), target => InfrastructureLayer().IsMatch(target))),
        new(
            "SharedKernelDependsOnNoModule",
            "Common is shared by every module, so it can't depend on any of them.",
            code => Dependencies(code, SharedKernel(), target => ModuleNamespace().IsMatch(target))),
        new(
            "ModulesAreIsolated",
            "A module may use another module's IntegrationEvents and PublicApi only, so modules can change independently.",
            ModuleIsolation),
        new(
            "HandlersAreNotPublic",
            "Handlers are reached through IHandler<,>; a public handler invites other modules to call it directly.",
            PublicHandlers),
        new(
            "NoMachineClock",
            "Time comes from TimeProvider, so tests can control it (DateTime.Now and friends can't be faked).",
            MachineClockCalls),
        new(
            "IntegrationEventsSetEveryProperty",
            "An event property no constructor sets is always empty on the wire.",
            UnsetEventProperties),
    ];

    private static Regex Layer(string layer) =>
        new($@"^(Common|Modules\.\w+)\.{layer}(\.|$)", RegexOptions.CultureInvariant);

    [GeneratedRegex(@"^(Common|Modules\.\w+)\.(Application|Infrastructure|Presentation)(\.|$)")]
    private static partial Regex OuterLayer();

    [GeneratedRegex(@"^(Common|Modules\.\w+)\.(Infrastructure|Presentation)(\.|$)")]
    private static partial Regex InfrastructureOrPresentation();

    [GeneratedRegex(@"^(Common|Modules\.\w+)\.Infrastructure(\.|$)")]
    private static partial Regex InfrastructureLayer();

    [GeneratedRegex(@"^Microsoft\.(AspNetCore|EntityFrameworkCore|Extensions\.DependencyInjection)\.")]
    private static partial Regex Framework();

    [GeneratedRegex(@"^Microsoft\.(AspNetCore|EntityFrameworkCore)\.")]
    private static partial Regex AspNetCoreOrEf();

    [GeneratedRegex(@"^Common\.")]
    private static partial Regex SharedKernel();

    [GeneratedRegex(@"^Modules\.")]
    private static partial Regex ModuleNamespace();

    [GeneratedRegex(@"^Modules\.(?<module>\w+)\.(?<layer>\w+)")]
    private static partial Regex ModuleAndLayer();

    [GeneratedRegex(@"^System\.(DateTime|DateTimeOffset) System\.(DateTime|DateTimeOffset)::get_(Now|UtcNow|Today)\(\)$")]
    private static partial Regex MachineClock();

    [GeneratedRegex(@"\.IntegrationEvents(\.|$)")]
    private static partial Regex IntegrationEventsNamespace();

    /// <summary>Every dependency from a type whose full name matches <paramref name="from"/> to a type the test rejects.</summary>
    private static List<string> Dependencies(CheckedCode code, Regex from, Func<string, bool> forbidden) =>
        [.. code.Architecture.Types
            .Where(type => from.IsMatch(type.FullName))
            .SelectMany(type => type.Dependencies
                .Where(dependency => forbidden(dependency.Target.FullName))
                .Select(dependency => $"{type.FullName} depends on {dependency.Target.FullName}"))
            .Distinct()];

    private static List<string> ModuleIsolation(CheckedCode code) =>
        [.. code.Architecture.Types
            .Select(type => (type, from: ModuleAndLayer().Match(type.FullName)))
            .Where(source => source.from.Success)
            .SelectMany(source => source.type.Dependencies
                .Select(dependency => (dependency, to: ModuleAndLayer().Match(dependency.Target.FullName)))
                .Where(target => target.to.Success
                    && target.to.Groups["module"].Value != source.from.Groups["module"].Value
                    && target.to.Groups["layer"].Value is not ("IntegrationEvents" or "PublicApi"))
                .Select(target => $"{source.type.FullName} depends on {target.dependency.Target.FullName}"))
            .Distinct()];

    private static List<string> PublicHandlers(CheckedCode code) =>
        [.. code.Architecture.Classes
            .Where(type => type.Visibility == Visibility.Public
                && type.Attributes.Any(attribute => attribute.FullName == "Immediate.Handlers.Shared.HandlerAttribute"))
            .Select(type => $"{type.FullName} is a public handler")];

    private static List<string> MachineClockCalls(CheckedCode code) =>
        [.. code.Architecture.Types
            .SelectMany(type => type.Dependencies.OfType<MethodCallDependency>()
                .Where(call => MachineClock().IsMatch(call.TargetMember.FullName))
                .Select(call => $"{type.FullName} calls {call.TargetMember.FullName}"))
            .Distinct()];

    /// <summary>
    /// Builds each integration event through its public constructors with non-default sample
    /// arguments, then requires every public property to hold a value. Checking behaviour rather than
    /// names covers chained constructors and parameters named differently from their property.
    /// </summary>
    private static List<string> UnsetEventProperties(CheckedCode code) =>
        [.. code.Assemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type is { IsClass: true, IsPublic: true, IsAbstract: false }
                && IntegrationEventsNamespace().IsMatch(type.Namespace ?? string.Empty))
            .SelectMany(UnsetProperties)];

    private static IEnumerable<string> UnsetProperties(Type type)
    {
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.GetIndexParameters().Length == 0)
            .ToList();
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var constructor in type.GetConstructors())
        {
            var instance = constructor.Invoke([.. constructor.GetParameters().Select(p => SampleValue(p.ParameterType, 0))]);
            set.UnionWith(properties.Where(property => !IsDefault(property.GetValue(instance))).Select(property => property.Name));
        }

        return properties.Where(property => !set.Contains(property.Name))
            .Select(property => $"{type.FullName}.{property.Name} is not set by its constructor");
    }

    private static bool IsDefault(object? value) =>
        value is null || (value.GetType().IsValueType && value.Equals(Activator.CreateInstance(value.GetType())));

    /// <summary>A value of <paramref name="type"/> that differs from its default.</summary>
    private static object? SampleValue(Type type, int depth)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        if (underlying == typeof(string))
        {
            return "sample";
        }

        if (underlying == typeof(Guid))
        {
            return Guid.NewGuid();
        }

        if (underlying == typeof(bool))
        {
            return true;
        }

        if (underlying == typeof(DateTimeOffset))
        {
            return new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        }

        if (underlying == typeof(TimeSpan))
        {
            return TimeSpan.FromMinutes(1);
        }

        if (underlying.IsEnum)
        {
            var values = System.Enum.GetValues(underlying);
            return values.Length > 1 ? values.GetValue(1) : System.Enum.ToObject(underlying, 1);
        }

        if (underlying.IsPrimitive || underlying == typeof(decimal))
        {
            return Convert.ChangeType(1, underlying, System.Globalization.CultureInfo.InvariantCulture);
        }

        if (underlying.IsArray)
        {
            var element = underlying.GetElementType()!;
            var array = Array.CreateInstance(element, 1);
            array.SetValue(SampleValue(element, depth + 1), 0);
            return array;
        }

        if (underlying.IsGenericType && underlying.GetGenericArguments() is [var item]
            && underlying.IsAssignableFrom(item.MakeArrayType()))
        {
            return SampleValue(item.MakeArrayType(), depth + 1);
        }

        var constructor = underlying.GetConstructors().MaxBy(c => c.GetParameters().Length);
        return depth < 3 && constructor is not null
            ? constructor.Invoke([.. constructor.GetParameters().Select(p => SampleValue(p.ParameterType, depth + 1))])
            : null;
    }
}
