using Common.Presentation.Idempotency;
using Microsoft.OpenApi;

namespace Api;

internal static class OpenApiSetup
{
    /// <summary>The one document, served at /openapi/v1.json in development and written by the build.</summary>
    public const string DocumentName = "v1";

    public static IServiceCollection AddApiOpenApi(this IServiceCollection services, IConfiguration configuration)
    {
        var title = configuration["Api:Title"];
        return services.AddOpenApi(DocumentName, options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info.Title = string.IsNullOrWhiteSpace(title) ? "API" : title;
                document.Info.Version = DocumentName;
                return Task.CompletedTask;
            });

            // An endpoint that takes an Idempotency-Key says so, so generated clients can send one.
            options.AddOperationTransformer((operation, context, _) =>
            {
                var idempotency = context.Description.ActionDescriptor.EndpointMetadata.OfType<IdempotencyMetadata>().FirstOrDefault();
                if (idempotency is not null)
                {
                    operation.Parameters ??= [];
                    operation.Parameters.Add(new OpenApiParameter
                    {
                        Name = IdempotencyEndpoints.HeaderName,
                        In = ParameterLocation.Header,
                        Required = idempotency.Required,
                        Description = "A unique value per request. Retrying with the same key (and the same request) runs it once and returns the first response, marked Idempotency-Replayed: true.",
                        Schema = new OpenApiSchema { Type = JsonSchemaType.String, MaxLength = 255 },
                    });
                }

                return Task.CompletedTask;
            });
        });
    }
}
