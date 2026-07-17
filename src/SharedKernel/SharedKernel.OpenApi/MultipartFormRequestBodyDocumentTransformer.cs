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
                description => OpenApiOperationKey.Create(description.RelativePath ?? string.Empty, description.HttpMethod ?? string.Empty),
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
                if (!TryGetMultipartSchema(operationItem.Value, out var schema) || schema is null)
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
        var (existingProperties, existingRequiredFields) = CollectExistingMultipartFields(schema);
        var (properties, requiredFields) = ResetMultipartSchema(schema);
        await PopulateFormParameters(
            properties,
            requiredFields,
            formParameters,
            existingProperties,
            existingRequiredFields,
            context,
            cancellationToken).ConfigureAwait(false);

        if (requiredFields.Count == 0)
        {
            schema.Required = null;
        }
    }

    private static (Dictionary<string, IOpenApiSchema> Properties, HashSet<string> RequiredFields) CollectExistingMultipartFields(OpenApiSchema schema)
    {
        var properties = schema.Properties is null
            ? new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal)
            : new Dictionary<string, IOpenApiSchema>(schema.Properties, StringComparer.Ordinal);
        var requiredFields = schema.Required is null
            ? new HashSet<string>(StringComparer.Ordinal)
            : new HashSet<string>(schema.Required, StringComparer.Ordinal);

        foreach (var entry in schema.AllOf?.OfType<OpenApiSchema>() ?? [])
        {
            CopyProperties(entry, properties);
            CopyRequiredFields(entry, requiredFields);
        }

        return (properties, requiredFields);
    }

    private static void CopyProperties(OpenApiSchema schema, Dictionary<string, IOpenApiSchema> properties)
    {
        if (schema.Properties is null)
        {
            return;
        }

        foreach (var property in schema.Properties)
        {
            properties.TryAdd(property.Key, property.Value);
        }
    }

    private static void CopyRequiredFields(OpenApiSchema schema, HashSet<string> requiredFields)
    {
        if (schema.Required is not null)
        {
            requiredFields.UnionWith(schema.Required);
        }
    }

    private static (Dictionary<string, IOpenApiSchema> Properties, HashSet<string> RequiredFields) ResetMultipartSchema(OpenApiSchema schema)
    {
        var properties = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal);
        var requiredFields = new HashSet<string>(StringComparer.Ordinal);

        schema.Type = JsonSchemaType.Object;
        schema.AllOf = null;
        schema.AnyOf = [];
        schema.OneOf = [];
        schema.Properties = properties;
        schema.Required = requiredFields;

        return (properties, requiredFields);
    }

    private static async Task PopulateFormParameters(
        Dictionary<string, IOpenApiSchema> properties,
        HashSet<string> requiredFields,
        IReadOnlyCollection<ApiParameterDescription> formParameters,
        Dictionary<string, IOpenApiSchema> existingProperties,
        HashSet<string> existingRequiredFields,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        foreach (var parameter in formParameters)
        {
            if (string.IsNullOrWhiteSpace(parameter.Name) || parameter.Type is null)
            {
                continue;
            }

            var parameterSchema = existingProperties.TryGetValue(parameter.Name, out var existingSchema)
                ? existingSchema
                : await context.GetOrCreateSchemaAsync(parameter.Type, parameter, cancellationToken).ConfigureAwait(false);
            properties[parameter.Name] = parameterSchema;

            if (existingRequiredFields.Contains(parameter.Name) || IsRequired(parameter))
            {
                requiredFields.Add(parameter.Name);
            }
        }
    }

    private static bool TryGetMultipartSchema(OpenApiOperation operation, out OpenApiSchema? schema)
    {
        schema = null;

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
