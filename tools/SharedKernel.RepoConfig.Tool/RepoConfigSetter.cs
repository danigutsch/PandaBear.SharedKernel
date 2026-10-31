using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PandaBear.RepoConfig.Tool;

/// <summary>Updates supported keys of the roadmap configuration file.</summary>
internal static class RepoConfigSetter
{
    /// <summary>Configuration key that holds the GitHub repository.</summary>
    public const string GitHubRepositoryKey = "github.repository";

    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = true
    };

    /// <summary>Sets the configuration key to the value in the configuration file.</summary>
    /// <param name="rootPath">Repository root.</param>
    /// <param name="key">Configuration key.</param>
    /// <param name="value">New value.</param>
    /// <exception cref="ArgumentException"><paramref name="rootPath"/>, <paramref name="key"/>, or <paramref name="value"/> is empty or whitespace, <paramref name="key"/> is unsupported, or <paramref name="value"/> is not shaped as <c>owner/repository</c> for <c>github.repository</c>.</exception>
    /// <exception cref="FileNotFoundException">The roadmap config file does not exist.</exception>
    /// <exception cref="InvalidOperationException">The roadmap config root is not a JSON object.</exception>
    public static void Set(string rootPath, string key, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        if (!string.Equals(key, GitHubRepositoryKey, StringComparison.Ordinal))
        {
            throw new ArgumentException($"Unsupported config key: {key}", nameof(key));
        }

        if (!GitHubRepositoryName.IsValid(value))
        {
            throw new ArgumentException("github.repository must be shaped as owner/repository.", nameof(value));
        }

        var configPath = Path.GetFullPath(Path.Combine(rootPath, RepoConfigPaths.Config));
        RollbackFileWriteBatch.ValidateWritePath(rootPath, configPath);
        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException("Roadmap config file was not found.", RepoConfigPaths.Config);
        }

        var original = File.ReadAllText(configPath);
        var root = JsonNode.Parse(original) as JsonObject
            ?? throw new InvalidOperationException("Roadmap config root must be a JSON object.");

        var integrations = GetOrCreateObject(root, "integrations");
        var github = GetOrCreateObject(integrations, "github");
        github["repository"] = value;

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, WriterOptions))
        {
            root.WriteTo(writer);
        }

        RollbackFileWriteBatch.Apply(rootPath, [new(configPath, Encoding.UTF8.GetString(stream.ToArray()) + "\n", original)]);
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
