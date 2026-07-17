using Microsoft.AspNetCore.Routing.Patterns;

namespace SharedKernel.OpenApi;

internal static class OpenApiOperationKey
{
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
