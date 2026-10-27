using System.Reflection;
using ArchUnitNET.Loader;

namespace ArchitectureTests;

/// <summary>Every rule holds for the app: the host, the shared kernel and every module.</summary>
public sealed class AppRuleTests
{
    private static readonly Lazy<CheckedCode> App = new(() =>
    {
        var assemblies = Directory.GetFiles(AppContext.BaseDirectory, "*.dll")
            .Where(path => Path.GetFileName(path) is var name
                && (name == "Api.dll" || name.StartsWith("Common.", StringComparison.Ordinal)
                    || name.StartsWith("Modules.", StringComparison.Ordinal)))
            .Select(Assembly.LoadFrom)
            .ToList();
        return new CheckedCode(new ArchLoader().LoadAssemblies([.. assemblies]).Build(), assemblies);
    });

    internal static CheckedCode Code => App.Value;

    [TestCaseSource(typeof(HouseRules), nameof(HouseRules.All))]
    public void Rule_holds(ArchitectureRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        Assert.That(rule.Violations(App.Value), Is.Empty, rule.Why);
    }

    [Test]
    public void The_app_is_checked() =>
        Assert.That(
            App.Value.Assemblies.Select(assembly => assembly.GetName().Name),
            Does.Contain("Api").And.Contain("Common.Domain").And.Contain("Modules.Platform.Application"));
}
