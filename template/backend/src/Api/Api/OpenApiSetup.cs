namespace Api;

internal static class OpenApiSetup
{
    /// <summary>The one document, served at /openapi/v1.json in development and written by the build.</summary>
    public const string DocumentName = "v1";

    public static IServiceCollection AddApiOpenApi(this IServiceCollection services, IConfiguration configuration)
    {
        var title = configuration["Api:Title"];
        return services.AddOpenApi(DocumentName, options => options.AddDocumentTransformer((document, _, _) =>
        {
            document.Info.Title = string.IsNullOrWhiteSpace(title) ? "API" : title;
            document.Info.Version = DocumentName;
            return Task.CompletedTask;
        }));
    }
}
