using Microsoft.AspNetCore.Routing.Patterns;

namespace PandaBear.OpenApi;

/// <summary>Builds the key that identifies an OpenAPI operation by method and normalized route.</summary>
internal static class OpenApiOperationKey
{
    /// <summary>Returns "METHOD:path" with route parameters reduced to their names, so keys match regardless of constraints and defaults.</summary>
    public static string Create(string relativePath, string httpMethod)
    {
        var pattern = RoutePatternFactory.Parse(relativePath.Trim('/'));
        var path = string.Join(
            "/",
            pattern.PathSegments.Select(static segment => string.Concat(segment.Parts.Select(FormatPart))));
        return $"{httpMethod}:{path}";
    }

    private static string FormatPart(RoutePatternPart part) => part switch
    {
        RoutePatternLiteralPart literal => literal.Content,
        RoutePatternParameterPart parameter => $"{{{parameter.Name}}}",
        RoutePatternSeparatorPart separator => separator.Content,
        _ => throw new InvalidOperationException($"Unsupported route pattern part '{part.GetType().Name}'.")
    };
}
