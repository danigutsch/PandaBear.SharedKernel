using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SharedKernel.RepoConfig.Tool;

internal static class RepoConfigSetter
{
    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = true
    };

    public static void Set(string rootPath, string key, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        if (!string.Equals(key, "github.repository", StringComparison.Ordinal))
        {
            throw new ArgumentException($"Unsupported config key: {key}", nameof(key));
        }

        if (!GitHubRepositoryName.IsValid(value))
        {
            throw new ArgumentException("github.repository must be shaped as owner/repository.", nameof(value));
        }

        var configPath = Path.Combine(rootPath, RepoConfigPaths.Config);
        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException("Roadmap config file was not found.", RepoConfigPaths.Config);
        }

        var root = JsonNode.Parse(File.ReadAllText(configPath)) as JsonObject
            ?? throw new InvalidOperationException("Roadmap config root must be a JSON object.");

        var integrations = GetOrCreateObject(root, "integrations");
        var github = GetOrCreateObject(integrations, "github");
        github["repository"] = value;

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, WriterOptions))
        {
            root.WriteTo(writer);
        }

        File.WriteAllText(configPath, Encoding.UTF8.GetString(stream.ToArray()) + "\n");
    }

    private static JsonObject GetOrCreateObject(JsonObject parent, string propertyName)
    {
        if (parent[propertyName] is JsonObject existing)
        {
            return existing;
        }

        var created = new JsonObject();
        parent[propertyName] = created;
        return created;
    }
}
