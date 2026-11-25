using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using Microsoft.OpenApi;

namespace PandaBear.OpenApi;

/// <summary>Documents response locations for endpoints that return resource or operation URIs.</summary>
public static class LocationResponseEndpointExtensions
{
    /// <summary>Documents the required <c>Location</c> header of an existing response as a URI reference.</summary>
    /// <typeparam name="TBuilder">The endpoint convention builder type.</typeparam>
    /// <param name="builder">The endpoint or group builder.</param>
    /// <param name="description">The header description, or <see langword="null"/> for the created-resource description.</param>
    /// <param name="statusCode">The response status that carries the header. Defaults to 201 Created.</param>
    /// <returns>The same builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    public static TBuilder WithLocationResponseHeader<TBuilder>(this TBuilder builder, string? description = null, int statusCode = StatusCodes.Status201Created)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        var responseKey = statusCode.ToString(CultureInfo.InvariantCulture);
        return builder.AddOpenApiOperationTransformer((operation, _, _) =>
        {
            if (operation.Responses?.TryGetValue(responseKey, out var documented) == true && documented is OpenApiResponse response)
            {
                response.Headers ??= new Dictionary<string, IOpenApiHeader>(StringComparer.Ordinal);
                response.Headers[HeaderNames.Location] = new OpenApiHeader
                {
                    Description = description ?? "URI reference of the created resource.",
                    Required = true,
                    Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "uri-reference" }
                };
            }

            return Task.CompletedTask;
        });
    }
}
