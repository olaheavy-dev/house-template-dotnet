using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests;

/// <summary>
/// The API in memory, with <paramref name="configure"/> changing its services after the app's own.
/// WebApplicationFactory runs it as Development unless <paramref name="environment"/> says otherwise;
/// <paramref name="settings"/> are configuration values, as if from the environment.
/// </summary>
internal sealed class TestApi(
    Action<IServiceCollection> configure,
    string? environment = null,
    IReadOnlyDictionary<string, string>? settings = null) : WebApplicationFactory<Program>
{
    public TestApi(string environment)
        : this(_ => { }, environment)
    {
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        if (environment is not null)
        {
            builder.UseEnvironment(environment);
        }

        builder.UseSetting("ConnectionStrings:Default", TestDatabase.ConnectionString);
        // What appsettings.json carries in a generated project (the template ships it as appsettings.json.jinja).
        builder.UseSetting("feature_management:feature_flags:0:id", "notes-list");
        builder.UseSetting("feature_management:feature_flags:0:enabled", "true");
        builder.UseSetting("Flags:Public:0", "notes-list");
        foreach (var (key, value) in settings ?? new Dictionary<string, string>())
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureTestServices(configure);
    }
}
