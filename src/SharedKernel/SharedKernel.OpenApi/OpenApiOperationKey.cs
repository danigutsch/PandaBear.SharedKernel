using Microsoft.AspNetCore.Routing.Patterns;

namespace PandaBear.OpenApi;

/// <summary>Builds the key that identifies an OpenAPI operation by method and normalized route.</summary>
internal readonly record struct OpenApiOperationKey
{
    private readonly string? value;

    private OpenApiOperationKey(string value) => this.value = value;

    /// <summary>Compares normalized operation identities without case sensitivity.</summary>
    public bool Equals(OpenApiOperationKey other) => StringComparer.OrdinalIgnoreCase.Equals(value, other.value);

    /// <inheritdoc />
    public override int GetHashCode() => value is null ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(value);

    /// <summary>Returns "METHOD:path" with route parameters reduced to their names, so keys match regardless of constraints and defaults.</summary>
    public static OpenApiOperationKey Create(string relativePath, string httpMethod)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(httpMethod);
        // HttpMethod validates the complete HTTP token grammar, including embedded whitespace and controls.
        try
        {
            _ = new HttpMethod(httpMethod);
        }
        catch (FormatException exception)
        {
            throw new ArgumentException("The HTTP method must be a valid token.", nameof(httpMethod), exception);
        }
        var pattern = RoutePatternFactory.Parse(relativePath.Trim('/'));
        var path = string.Join(
            "/",
            pattern.PathSegments.Select(static segment => string.Concat(segment.Parts.Select(FormatPart))));
        return new OpenApiOperationKey($"{httpMethod}:{path}");
    }

    private static string FormatPart(RoutePatternPart part) => part switch
    {
        RoutePatternLiteralPart literal => literal.Content,
        RoutePatternParameterPart parameter => $"{{{parameter.Name}}}",
        RoutePatternSeparatorPart separator => separator.Content,
        _ => throw new InvalidOperationException($"Unsupported route pattern part '{part.GetType().Name}'.")
    };
}
