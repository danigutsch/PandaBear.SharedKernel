using System.Text.Json;

namespace PandaBear.Documentation;

/// <summary>Reads positive operation authorization evidence from configured OpenAPI documents.</summary>
internal sealed class OpenApiRouteAuthorization(
    IReadOnlyList<(string Prefix, HashSet<(string Name, string Method, string Path)> ProtectedOperations)> scopes)
{
    private static readonly HashSet<string> OperationMethods = new(StringComparer.Ordinal)
    {
        "get", "put", "post", "delete", "options", "head", "patch", "trace", "query",
    };

    private static readonly HashSet<string> SecuritySchemeTypes = new(StringComparer.Ordinal)
    {
        "apiKey", "http", "mutualTLS", "oauth2", "openIdConnect",
    };

    /// <summary>Reads one generated OpenAPI document for each configured API source prefix.</summary>
    /// <param name="rootPath">The absolute documentation repository root.</param>
    /// <param name="documents">The source prefixes and repository-relative OpenAPI artifact paths.</param>
    /// <returns>The authorization evidence available for the configured source prefixes.</returns>
    /// <exception cref="ArgumentNullException">The document mapping is null.</exception>
    public static OpenApiRouteAuthorization Load(string rootPath, IReadOnlyDictionary<string, string> documents)
    {
        ArgumentNullException.ThrowIfNull(documents);

        return new OpenApiRouteAuthorization(documents.Select(document =>
        {
            var prefix = document.Key.TrimEnd('/');
            if (string.IsNullOrWhiteSpace(prefix) || string.IsNullOrWhiteSpace(document.Value))
            {
                throw new InvalidOperationException("OpenAPI authorization evidence requires a source prefix and a document path.");
            }

            return (prefix + "/", LoadDocument(Path.GetFullPath(document.Value, rootPath)));
        }).ToArray());
    }

    /// <summary>Determines whether exactly one matching scope and operation provides positive authorization evidence.</summary>
    /// <param name="source">The source path relative to the inventory's source directory.</param>
    /// <param name="name">The explicitly declared operation name.</param>
    /// <param name="method">The uppercase HTTP method.</param>
    /// <param name="path">The route template as rendered in the source inventory.</param>
    /// <returns>Whether an unambiguous matching OpenAPI operation requires authorization.</returns>
    public bool RequiresAuthorization(string source, string name, string method, string path)
    {
        var matches = scopes.Where(scope => source.StartsWith(scope.Prefix, StringComparison.Ordinal)).Take(2).ToArray();
        return matches.Length == 1 && matches[0].ProtectedOperations.Contains((name, method, path));
    }

    private static HashSet<(string Name, string Method, string Path)> LoadDocument(string path)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions
            {
                AllowDuplicateProperties = false,
            });
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("openapi", out var version)
                || version.ValueKind != JsonValueKind.String
                || !Version.TryParse(version.GetString(), out var parsedVersion)
                || parsedVersion.Major != 3
                || !root.TryGetProperty("paths", out var paths)
                || paths.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException($"OpenAPI authorization document '{path}' requires an OpenAPI 3 version and a paths object.");
            }

            var schemes = GetSecuritySchemes(root);
            return ReadOperations(paths)
                .GroupBy(operation => operation.Name, StringComparer.Ordinal)
                .Where(group => group.Count() == 1)
                .Select(group => group.Single())
                .Where(operation => IsProtected(operation.Definition, schemes))
                .Select(operation => (operation.Name, operation.Method, operation.Path))
                .ToHashSet();
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException($"OpenAPI authorization document '{path}' contains invalid JSON.", exception);
        }
    }

    private static JsonElement GetSecuritySchemes(JsonElement root) =>
        root.TryGetProperty("components", out var components) && components.ValueKind == JsonValueKind.Object
            && components.TryGetProperty("securitySchemes", out var schemes) ? schemes : default;

    private static IEnumerable<(string Name, string Method, string Path, JsonElement Definition)> ReadOperations(JsonElement paths)
    {
        foreach (var path in paths.EnumerateObject())
        {
            if (path.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            foreach (var operation in path.Value.EnumerateObject())
            {
                if (OperationMethods.Contains(operation.Name)
                    && operation.Value.ValueKind == JsonValueKind.Object
                    && operation.Value.TryGetProperty("operationId", out var name)
                    && name.ValueKind == JsonValueKind.String
                    && name.GetString() is { Length: > 0 } operationId)
                {
                    yield return (operationId, operation.Name.ToUpperInvariant(), path.Name, operation.Value);
                }
            }
        }
    }

    private static bool IsProtected(JsonElement operation, JsonElement schemes) =>
        schemes.ValueKind == JsonValueKind.Object
        && operation.TryGetProperty("security", out var security)
        && security.ValueKind == JsonValueKind.Array
        && security.GetArrayLength() > 0
        && security.EnumerateArray().All(alternative =>
            alternative.ValueKind == JsonValueKind.Object
            && alternative.EnumerateObject().Any()
            && alternative.EnumerateObject().All(requirement => IsDeclaredRequirement(requirement, schemes)));

    private static bool IsDeclaredRequirement(JsonProperty requirement, JsonElement schemes) =>
        requirement.Value.ValueKind == JsonValueKind.Array
        && requirement.Value.EnumerateArray().All(scope => scope.ValueKind == JsonValueKind.String)
        && schemes.TryGetProperty(requirement.Name, out var scheme)
        && scheme.ValueKind == JsonValueKind.Object
        && scheme.TryGetProperty("type", out var type)
        && type.ValueKind == JsonValueKind.String
        && type.GetString() is { } schemeType
        && SecuritySchemeTypes.Contains(schemeType);
}
