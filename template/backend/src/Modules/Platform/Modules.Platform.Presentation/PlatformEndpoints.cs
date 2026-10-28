using Microsoft.AspNetCore.Routing;

namespace Modules.Platform.Presentation;

/// <summary>The module's endpoints, mapped by the host. A new endpoint adds its call here.</summary>
public static class PlatformEndpoints
{
    public static IEndpointRouteBuilder MapPlatformEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPing();
        app.MapPingStream();
        app.MapMe();
        return app;
    }
}
