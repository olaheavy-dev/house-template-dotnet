using Microsoft.AspNetCore.Routing;

namespace Modules.Sample.Presentation;

/// <summary>The module's endpoints, mapped by the host. A new endpoint adds its call here.</summary>
public static class SampleEndpoints
{
    public static IEndpointRouteBuilder MapSampleEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapStatus();
        return app;
    }
}
