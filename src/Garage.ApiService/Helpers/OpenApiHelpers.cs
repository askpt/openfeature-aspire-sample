using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

internal static class OpenApiHelpers
{
    internal static void ConfigureOpenApi(OpenApiOptions options)
    {
        options.AddDocumentTransformer((document, _, _) =>
        {
            document.Info.Description = "REST API for Le Mans winners data with feature-flag-driven behavior and caching.";
            document.Info.Title = "Garage API Service";
            document.Info.Version = "1.0.0";
            document.Tags = new HashSet<OpenApiTag>
            {
            new()
            {
                Name = "Le Mans Winners",
                Description = "Operations for retrieving Le Mans winners and related vehicle details."
            }
            };

            if (document.Components?.Schemas is { } schemas)
            {
                if (schemas.TryGetValue("ProblemDetails", out var problemDetailsSchema) &&
                    problemDetailsSchema.Properties is { } problemProperties)
                {
                    SetSchemaPropertyDescription(problemProperties, "type", "A URI reference that identifies the problem type.");
                    SetSchemaPropertyDescription(problemProperties, "title", "A short, human-readable summary of the problem.");
                    SetSchemaPropertyDescription(problemProperties, "status", "The HTTP status code generated for this problem.");
                    SetSchemaPropertyDescription(problemProperties, "detail", "A human-readable explanation specific to this problem instance.");
                    SetSchemaPropertyDescription(problemProperties, "instance", "A URI reference that identifies this specific problem occurrence.");
                }
            }

            return Task.CompletedTask;
        });

        options.AddOperationTransformer((operation, _, _) =>
        {
            if (operation.OperationId == "GetAllLeMansWinners" && operation.Responses is { } responses)
            {
                if (responses.TryGetValue("200", out var okResponse))
                {
                    okResponse.Description = "Le Mans winners were successfully retrieved.";
                }

                if (responses.TryGetValue("500", out var errorResponse))
                {
                    errorResponse.Description = "An unexpected server error occurred while retrieving winners.";
                }
            }

            return Task.CompletedTask;
        });
    }

    static void SetSchemaPropertyDescription(IDictionary<string, IOpenApiSchema> properties, string propertyName, string description)
    {
        if (properties.TryGetValue(propertyName, out var schema))
        {
            schema.Description = description;
        }
    }
}
