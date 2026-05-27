using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

namespace SharedKernel.OpenApi;

/// <summary>
/// Rebuilds multipart form request-body schemas from form-bound API parameters.
/// </summary>
/// <remarks>
/// ASP.NET Core can emit invalid multipart schemas for some minimal-API form-binding
/// shapes. This transformer provides a reusable, project-agnostic normalization step
/// for any generated OpenAPI document that exposes multipart form data.
/// </remarks>
public sealed class MultipartFormRequestBodyDocumentTransformer : IOpenApiDocumentTransformer
{
    private const string MultipartFormDataContentType = "multipart/form-data";

    /// <inheritdoc />
    public async Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(context);

        var optionsMonitor = context.ApplicationServices.GetService(typeof(IOptionsMonitor<OpenApiOptions>)) as IOptionsMonitor<OpenApiOptions>
            ?? throw new InvalidOperationException("OpenApiOptions were not available from the document transformer service provider.");
        var options = optionsMonitor.Get(context.DocumentName);

        var descriptions = context.DescriptionGroups
            .SelectMany(static group => group.Items)
            .Where(description => options.ShouldInclude(description))
            .Where(static description => !string.IsNullOrWhiteSpace(description.RelativePath) && !string.IsNullOrWhiteSpace(description.HttpMethod))
            .ToDictionary(
                description => CreateKey(description.RelativePath!, description.HttpMethod!),
                description => description,
                StringComparer.OrdinalIgnoreCase);

        foreach (var pathItem in document.Paths)
        {
            if (pathItem.Value.Operations is null)
            {
                continue;
            }

            foreach (var operationItem in pathItem.Value.Operations)
            {
                if (!TryGetMultipartSchema(operationItem.Value, out var schema))
                {
                    continue;
                }

                if (!descriptions.TryGetValue(CreateKey(pathItem.Key.TrimStart('/'), operationItem.Key.Method), out var description))
                {
                    continue;
                }

                var formParameters = description.ParameterDescriptions
                    .Where(static parameter => parameter.Source == BindingSource.Form || parameter.Source == BindingSource.FormFile)
                    .ToArray();

                if (formParameters.Length == 0)
                {
                    continue;
                }

                if (!RequiresMultipartSchemaNormalization(schema))
                {
                    continue;
                }

                schema.Type = JsonSchemaType.Object;
                schema.AllOf = [];
                schema.AnyOf = [];
                schema.OneOf = [];
                schema.Properties = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal);
                schema.Required = new HashSet<string>(StringComparer.Ordinal);

                foreach (var parameter in formParameters)
                {
                    if (string.IsNullOrWhiteSpace(parameter.Name) || parameter.Type is null)
                    {
                        continue;
                    }

                    var parameterSchema = await context.GetOrCreateSchemaAsync(parameter.Type, parameter, cancellationToken);
                    schema.Properties[parameter.Name] = parameterSchema;

                    if (parameter.IsRequired)
                    {
                        schema.Required.Add(parameter.Name);
                    }
                }
            }
        }
    }

    private static string CreateKey(string? relativePath, string? httpMethod)
    {
        return $"{httpMethod}:{relativePath}";
    }

    private static bool TryGetMultipartSchema(OpenApiOperation operation, out OpenApiSchema schema)
    {
        schema = null!;

        if (operation.RequestBody?.Content is null)
        {
            return false;
        }

        if (!operation.RequestBody.Content.TryGetValue(MultipartFormDataContentType, out var mediaType)
            || mediaType.Schema is not OpenApiSchema openApiSchema)
        {
            return false;
        }

        schema = openApiSchema;
        return true;
    }

    private static bool RequiresMultipartSchemaNormalization(OpenApiSchema schema)
    {
        if (schema.AllOf is null || schema.AllOf.Count == 0)
        {
            return false;
        }

        return schema.AllOf.Any(static item =>
            item is not OpenApiSchema nestedSchema
            || nestedSchema.Type != JsonSchemaType.Object
            || nestedSchema.Properties is null);
    }
}
