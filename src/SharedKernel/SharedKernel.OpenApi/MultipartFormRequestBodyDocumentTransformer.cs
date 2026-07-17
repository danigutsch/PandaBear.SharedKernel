using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
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
                description => OpenApiOperationKey.Create(description.RelativePath!, description.HttpMethod!),
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

                if (!descriptions.TryGetValue(OpenApiOperationKey.Create(pathItem.Key, operationItem.Key.Method), out var description))
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

                await NormalizeMultipartSchema(schema, formParameters, context, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static async Task NormalizeMultipartSchema(
        OpenApiSchema schema,
        IReadOnlyCollection<ApiParameterDescription> formParameters,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        var existingProperties = schema.Properties is null
            ? new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal)
            : new Dictionary<string, IOpenApiSchema>(schema.Properties, StringComparer.Ordinal);
        var requiredFields = schema.Required is null
            ? new HashSet<string>(StringComparer.Ordinal)
            : new HashSet<string>(schema.Required, StringComparer.Ordinal);

        if (schema.AllOf is not null)
        {
            foreach (var entry in schema.AllOf.OfType<OpenApiSchema>())
            {
                if (entry.Properties is not null)
                {
                    foreach (var property in entry.Properties)
                    {
                        existingProperties.TryAdd(property.Key, property.Value);
                    }
                }

                if (entry.Required is not null)
                {
                    requiredFields.UnionWith(entry.Required);
                }
            }
        }

        schema.Type = JsonSchemaType.Object;
        schema.AllOf = null;
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

            var parameterSchema = existingProperties.TryGetValue(parameter.Name, out var existingSchema)
                ? existingSchema
                : await context.GetOrCreateSchemaAsync(parameter.Type, parameter, cancellationToken).ConfigureAwait(false);
            schema.Properties[parameter.Name] = parameterSchema;

            if (requiredFields.Contains(parameter.Name) || IsRequired(parameter))
            {
                schema.Required.Add(parameter.Name);
            }
        }

        if (schema.Required.Count == 0)
        {
            schema.Required = null;
        }
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

    private static bool IsRequired(ApiParameterDescription parameter)
    {
        return parameter.IsRequired
               || parameter.ModelMetadata.IsRequired
               || parameter.ModelMetadata.ValidatorMetadata.OfType<RequiredAttribute>().Any();
    }
}
