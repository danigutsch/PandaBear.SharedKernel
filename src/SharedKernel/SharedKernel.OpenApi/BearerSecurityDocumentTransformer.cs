using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

namespace SharedKernel.OpenApi;

/// <summary>
/// Documents JWT bearer authentication only for operations that are not explicitly anonymous.
/// </summary>
public sealed class BearerSecurityDocumentTransformer : IOpenApiDocumentTransformer
{
    /// <inheritdoc />
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>(StringComparer.Ordinal);
        document.Components.SecuritySchemes[OpenApiAuthenticationDefaults.BearerSecuritySchemeName] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT"
        };

        var optionsMonitor = context.ApplicationServices.GetService(typeof(IOptionsMonitor<OpenApiOptions>)) as IOptionsMonitor<OpenApiOptions>
            ?? throw new InvalidOperationException("OpenApiOptions were not available from the document transformer service provider.");
        var options = optionsMonitor.Get(context.DocumentName);
        var descriptions = context.DescriptionGroups
            .SelectMany(static group => group.Items)
            .Where(description => options.ShouldInclude(description))
            .Where(static description => !string.IsNullOrWhiteSpace(description.RelativePath) && !string.IsNullOrWhiteSpace(description.HttpMethod))
            .ToDictionary(
                description => CreateOperationKey(description.RelativePath!, description.HttpMethod!),
                description => description,
                StringComparer.OrdinalIgnoreCase);

        foreach (var path in document.Paths)
        {
            if (path.Value.Operations is null)
            {
                continue;
            }

            foreach (var operation in path.Value.Operations)
            {
                if (!descriptions.TryGetValue(CreateOperationKey(path.Key.TrimStart('/'), operation.Key.Method), out var description)
                    || description.ActionDescriptor.EndpointMetadata?.OfType<IAllowAnonymous>().Any() == true)
                {
                    continue;
                }

                operation.Value.Security =
                [
                    new OpenApiSecurityRequirement
                    {
                        [new OpenApiSecuritySchemeReference(OpenApiAuthenticationDefaults.BearerSecuritySchemeName, document)] = []
                    }
                ];
                operation.Value.Responses ??= [];
                operation.Value.Responses.TryAdd("401", new OpenApiResponse { Description = "Authentication is required." });
                operation.Value.Responses.TryAdd("403", new OpenApiResponse { Description = "The authenticated caller lacks the required permission." });
            }
        }

        return Task.CompletedTask;
    }

    private static string CreateOperationKey(string relativePath, string method)
    {
        return string.Concat(method, ":", relativePath.Trim('/'));
    }
}
