using System.Reflection;
using System.Xml.Linq;

namespace ArchitectureTests;

/// <summary>
/// The module layout, checked on the files: every module has its six projects, every project is
/// built, and the host registers every module. A step missed when adding a module fails here.
/// </summary>
public sealed class ConformanceTests
{
    private static readonly string[] ModuleLayers =
        ["Domain", "Application", "Infrastructure", "Presentation", "IntegrationEvents", "PublicApi"];

    private static readonly Lazy<DirectoryInfo> Backend = new(() =>
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Backend.slnx")))
        {
            directory = directory.Parent;
        }

        return directory ?? throw new InvalidOperationException("Backend.slnx not found above the test output.");
    });

    private static string BackendPath(params string[] parts) => Path.Combine([Backend.Value.FullName, .. parts]);

    private static IEnumerable<string> Modules() =>
        Directory.GetDirectories(BackendPath("src", "Modules")).Select(Path.GetFileName).OfType<string>().Order();

    [Test]
    public void There_is_at_least_one_module() => Assert.That(Modules(), Is.Not.Empty);

    [Test]
    public void Every_module_has_its_six_projects()
    {
        foreach (var module in Modules())
        {
            var projects = Directory.GetFiles(BackendPath("src", "Modules", module), "*.csproj", SearchOption.AllDirectories)
                .Select(Path.GetFileNameWithoutExtension);
            Assert.That(projects, Is.EquivalentTo(ModuleLayers.Select(layer => $"Modules.{module}.{layer}")), module);
        }
    }

    [Test]
    public void Every_project_is_in_the_solution()
    {
        var listed = XDocument.Load(BackendPath("Backend.slnx")).Descendants("Project")
            .Select(project => project.Attribute("Path")!.Value.Replace('\\', '/'))
            .ToHashSet();
        var projects = new[] { "src", "tests" }
            .SelectMany(root => Directory.GetFiles(BackendPath(root), "*.csproj", SearchOption.AllDirectories))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(Backend.Value.FullName, path).Replace('\\', '/'));
        Assert.That(listed, Is.SupersetOf(projects), "Add the project to Backend.slnx: one missing is never built or tested.");
    }

    [Test]
    public void The_host_references_and_registers_every_module()
    {
        var host = File.ReadAllText(BackendPath("src", "Api", "Api", "Api.csproj"));
        var program = File.ReadAllText(BackendPath("src", "Api", "Api", "Program.cs"));
        foreach (var module in Modules())
        {
            Assert.That(host, Does.Contain($@"Modules\{module}\Modules.{module}.Infrastructure\Modules.{module}.Infrastructure.csproj"), module);
            Assert.That(program, Does.Contain($"Add{module}Module("), module);
            Assert.That(program, Does.Contain($"Modules.{module}.Presentation.AssemblyReference.Assembly"), module);
        }
    }

    [Test]
    public void Every_module_declares_its_handler_pipeline()
    {
        foreach (var module in Modules())
        {
            var application = Assembly.Load($"Modules.{module}.Application");
            Assert.That(
                application.GetCustomAttributesData().Select(attribute => attribute.AttributeType.FullName),
                Does.Contain("Immediate.Handlers.Shared.BehaviorsAttribute"),
                $"{module}: add [assembly: Behaviors(typeof(LoggingBehavior<,>), typeof(ValidationBehavior<,>))]");
        }
    }

    [Test]
    public void One_build_settings_file_covers_every_project() =>
        Assert.That(
            Directory.GetFiles(Backend.Value.FullName, "Directory.Build.props", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(Backend.Value.FullName, path)),
            Is.EqualTo(new[] { "Directory.Build.props" }));
}
