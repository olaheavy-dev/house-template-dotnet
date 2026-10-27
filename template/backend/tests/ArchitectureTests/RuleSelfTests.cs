using ArchUnitNET.Loader;

namespace ArchitectureTests;

/// <summary>
/// Every rule finds the violation planted for it in ArchitectureTests.Fixtures. A rule that passes
/// against broken code would pass against anything.
/// </summary>
public sealed class RuleSelfTests
{
    private static readonly Lazy<CheckedCode> Broken = new(() =>
    {
        var assembly = typeof(Modules.Alpha.Domain.Order).Assembly;
        return new CheckedCode(new ArchLoader().LoadAssemblies(assembly).Build(), [assembly]);
    });

    private static readonly Dictionary<string, string> Planted = new()
    {
        ["DomainDependsOnNoOuterLayer"] = "Modules.Alpha.Domain.Order depends on Modules.Alpha.Application.OrderService",
        ["ApplicationDependsOnNoOuterLayer"] = "Modules.Alpha.Application.Saver depends on Modules.Alpha.Infrastructure.Repository",
        ["PresentationDependsOnNoInfrastructure"] = "Modules.Alpha.Presentation.Endpoint depends on Modules.Alpha.Infrastructure.Repository",
        ["SharedKernelDependsOnNoModule"] = "Common.Domain.Broken.Leak depends on Modules.Beta.Domain.Invoice",
        ["ModulesAreIsolated"] = "Modules.Alpha.Application.OrderService depends on Modules.Beta.Domain.Invoice",
        ["HandlersAreNotPublic"] = "Modules.Alpha.Application.PublicHandler is a public handler",
        ["NoMachineClock"] = "Modules.Alpha.Application.OrderService calls System.DateTimeOffset System.DateTimeOffset::get_UtcNow()",
        ["IntegrationEventsSetEveryProperty"] = "Modules.Beta.IntegrationEvents.InvoicePaid.Email is set by no constructor",
    };

    [TestCaseSource(typeof(HouseRules), nameof(HouseRules.All))]
    public void Rule_finds_its_planted_violation(ArchitectureRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        Assert.That(rule.Violations(Broken.Value), Does.Contain(Planted[rule.Name]));
    }

    [Test]
    public void Every_rule_has_a_planted_violation() =>
        Assert.That(Planted.Keys, Is.EquivalentTo(HouseRules.All.Select(rule => rule.Name)));
}
