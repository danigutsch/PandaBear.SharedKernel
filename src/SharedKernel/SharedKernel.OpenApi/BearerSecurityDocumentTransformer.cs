using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

namespace SharedKernel.OpenApi;

/// <summary>
/// Documents JWT bearer authentication only for operations protected by authorization metadata or a fallback policy.
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

        var optionsMonitor = context.ApplicationServices.GetService(typeof(IOptionsMonitor<OpenApiOptions>)) as IOptionsMonitor<OpenApiOptions>
            ?? throw new InvalidOperationException("OpenApiOptions were not available from the document transformer service provider.");
        var options = optionsMonitor.Get(context.DocumentName);
        var authorizationOptions = context.ApplicationServices.GetService(typeof(IOptions<AuthorizationOptions>)) as IOptions<AuthorizationOptions>;
        var hasFallbackAuthorizationPolicy = authorizationOptions?.Value.FallbackPolicy is not null;
        var descriptions = context.DescriptionGroups
            .SelectMany(static group => group.Items)
            .Where(description => options.ShouldInclude(description))
            .Where(static description => !string.IsNullOrWhiteSpace(description.RelativePath) && !string.IsNullOrWhiteSpace(description.HttpMethod))
            .ToDictionary(
                description => CreateOperationKey(description.RelativePath!, description.HttpMethod!),
                description => description,
                StringComparer.OrdinalIgnoreCase);

        var hasProtectedOperation = false;
        foreach (var path in document.Paths)
        {
            if (path.Value.Operations is null)
            {
                continue;
            }

            foreach (var operation in path.Value.Operations)
            {
                if (!descriptions.TryGetValue(CreateOperationKey(path.Key.TrimStart('/'), operation.Key.Method), out var description))
                {
                    continue;
                }

                var metadata = description.ActionDescriptor.EndpointMetadata;
                if (metadata?.OfType<IAllowAnonymous>().Any() == true
                    || (metadata?.OfType<IAuthorizeData>().Any() != true
                        && metadata?.OfType<AuthorizationPolicy>().Any() != true
                        && !hasFallbackAuthorizationPolicy))
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
                hasProtectedOperation = true;
            }
        }

        if (hasProtectedOperation)
        {
            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>(StringComparer.Ordinal);
            document.Components.SecuritySchemes[OpenApiAuthenticationDefaults.BearerSecuritySchemeName] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT"
            };
        }

        return Task.CompletedTask;
    }

    private static string CreateOperationKey(string relativePath, string method)
    {
        return string.Concat(method, ":", relativePath.Trim('/'));
    }
}
