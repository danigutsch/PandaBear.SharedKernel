using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using Microsoft.OpenApi;

namespace SharedKernel.OpenApi;

/// <summary>
/// Documents optimistic concurrency with entity tags: a read that returns an <c>ETag</c>, and a write that requires it back in
/// <c>If-Match</c> and answers 400, 412 or 428 problems (see <c>SharedKernel.HttpCaching.AspNetCore.IfMatchCondition</c>).
/// </summary>
public static class ConditionalRequestEndpointExtensions
{
    /// <summary>The default description of the <c>If-Match</c> request header.</summary>
    public const string IfMatchDescription = "The entity tag (ETag) from the latest read, or \"*\".";

    /// <summary>The default description of the <c>ETag</c> response header.</summary>
    public const string EntityTagDescription = "Strong validator of the returned representation; send it as If-Match on the next write.";

    private const string ProblemContentType = "application/problem+json";

    /// <summary>
    /// Documents a required <c>If-Match</c> request header and the problem responses of a conditional write: 400 for an unusable header,
    /// 412 Precondition Failed for a stale tag and 428 Precondition Required for a missing header.
    /// </summary>
    /// <typeparam name="TBuilder">The endpoint convention builder type.</typeparam>
    /// <param name="builder">The endpoint or group builder.</param>
    /// <param name="description">The header description; <see cref="IfMatchDescription"/> when <see langword="null"/>.</param>
    /// <returns>The same builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    public static TBuilder WithIfMatchPrecondition<TBuilder>(this TBuilder builder, string? description = null)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        var headerDescription = description ?? IfMatchDescription;
        builder.WithMetadata(
            Problem(StatusCodes.Status400BadRequest),
            Problem(StatusCodes.Status412PreconditionFailed),
            Problem(StatusCodes.Status428PreconditionRequired));
        return builder.AddOpenApiOperationTransformer((operation, _, _) =>
        {
            operation.Parameters ??= [];
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = HeaderNames.IfMatch,
                In = ParameterLocation.Header,
                Required = true,
                Description = headerDescription,
                Schema = new OpenApiSchema { Type = JsonSchemaType.String }
            });
            return Task.CompletedTask;
        });
    }

    /// <summary>Documents the required <c>ETag</c> header of a success response.</summary>
    /// <typeparam name="TBuilder">The endpoint convention builder type.</typeparam>
    /// <param name="builder">The endpoint or group builder.</param>
    /// <param name="description">The header description; <see cref="EntityTagDescription"/> when <see langword="null"/>.</param>
    /// <param name="statusCode">The documented response that carries the header. Default 200.</param>
    /// <returns>The same builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    public static TBuilder WithEntityTagResponseHeader<TBuilder>(this TBuilder builder, string? description = null, int statusCode = StatusCodes.Status200OK)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        var headerDescription = description ?? EntityTagDescription;
        var key = statusCode.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return builder.AddOpenApiOperationTransformer((operation, _, _) =>
        {
            if (operation.Responses is not null && operation.Responses.TryGetValue(key, out var documented) && documented is OpenApiResponse response)
            {
                response.Headers ??= new Dictionary<string, IOpenApiHeader>(StringComparer.Ordinal);
                response.Headers[HeaderNames.ETag] = new OpenApiHeader
                {
                    Description = headerDescription,
                    Required = true,
                    Schema = new OpenApiSchema { Type = JsonSchemaType.String }
                };
            }

            return Task.CompletedTask;
        });
    }

    private static ProducesResponseTypeMetadata Problem(int statusCode) =>
        new(statusCode, typeof(ProblemDetails), [ProblemContentType]);
}
