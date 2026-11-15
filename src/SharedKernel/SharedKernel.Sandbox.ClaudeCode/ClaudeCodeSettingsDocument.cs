using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PandaBear.Sandbox.ClaudeCode;

/// <summary>
/// The text of a Claude Code settings file as a JSON object. Every key that is not managed stays as it is, in order, and
/// the rewritten text keeps the file's indentation, line endings, and final newline.
/// </summary>
internal sealed class ClaudeCodeSettingsDocument : ISandboxSettingsDocument
{
    private const string EnvironmentKey = "env";

    private static readonly string[] AllowWritePath = ["sandbox", "filesystem", "allowWrite"];
    private static readonly string[] DenyWritePath = ["sandbox", "filesystem", "denyWrite"];
    private static readonly string[] DenyReadPath = ["sandbox", "filesystem", "denyRead"];
    private static readonly string[] DomainsPath = ["sandbox", "network", "allowedDomains"];
    private static readonly string[] ExcludedPath = ["sandbox", "excludedCommands"];
    private static readonly string[] SocketsPath = ["sandbox", "network", "allowAllUnixSockets"];
    private static readonly string[] EnvironmentPath = [EnvironmentKey];

    private readonly JsonObject _root;

    private ClaudeCodeSettingsDocument(string name, string? text, JsonObject root)
    {
        Name = name;
        Text = text;
        _root = root;
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public string? Text { get; }

    /// <summary>Parses <paramref name="text"/> as strict JSON: no comments, no trailing commas, and no repeated property.</summary>
    /// <param name="text">The file text, or <see langword="null"/> when the file does not exist. Empty or white-space text is an empty object.</param>
    /// <param name="name">The file name used in error messages.</param>
    /// <exception cref="InvalidOperationException">The text is not a JSON object. The message names the file and the JSON path.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    public static ClaudeCodeSettingsDocument Parse(string? text, string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (string.IsNullOrWhiteSpace(text))
        {
            return new ClaudeCodeSettingsDocument(name, text, []);
        }

        try
        {
            var node = JsonNode.Parse(
                text,
                nodeOptions: null,
                new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Disallow, AllowTrailingCommas = false, AllowDuplicateProperties = false });
            var root = node as JsonObject
                ?? throw new InvalidOperationException($"{name}: $ must be a JSON object.");
            return new ClaudeCodeSettingsDocument(name, text, root);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{name} is not valid JSON at {exception.Path ?? "$"}. Check for a repeated property, a comment, a trailing comma, or broken syntax."),
                exception);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidOperationException($"{name} is not valid JSON: a property is repeated or the nesting is too deep.", exception);
        }
    }

    /// <inheritdoc />
    public string LocationOf(SandboxSetting setting) => JsonPathOf(PathOf(setting));

    /// <inheritdoc />
    public IReadOnlyList<string> GetEntries(SandboxSetting setting)
    {
        var list = FindList(PathOf(setting));
        return list is null ? [] : [.. list.Select(static node => node!.GetValue<string>())];
    }

    /// <inheritdoc />
    public void AddEntry(SandboxSetting setting, string entry)
    {
        var path = PathOf(setting);
        var list = FindList(path) ?? CreateList(path);
        list.Add((JsonNode?)JsonValue.Create(entry));
    }

    /// <inheritdoc />
    public void RemoveEntry(SandboxSetting setting, int index)
    {
        var path = PathOf(setting);
        var list = FindList(path) ?? throw new ArgumentOutOfRangeException(nameof(index), index, "The list is absent.");
        list.RemoveAt(index);
    }

    /// <inheritdoc />
    public bool? GetFlag(SandboxSetting setting)
    {
        var path = PathOf(setting);
        JsonNode? node = null;
        _ = FindParent(path)?.TryGetPropertyValue(path[^1], out node);
        var enabled = false;
        return node is null || (node is JsonValue value && value.TryGetValue(out enabled)) ? enabled : null;
    }

    /// <inheritdoc />
    public void SetFlag(SandboxSetting setting)
    {
        var path = PathOf(setting);
        GetOrCreateParent(path)[path[^1]] = JsonValue.Create(true);
    }

    /// <inheritdoc />
    public bool TryGetEnvironmentVariable(string name, out string? value)
    {
        value = null;
        if (FindEnvironment() is not { } environment || !environment.TryGetPropertyValue(name, out var current) || current is null)
        {
            return false;
        }

        value = TryString(current, out var text) ? text : null;
        return true;
    }

    /// <inheritdoc />
    public void SetEnvironmentVariable(string name, string value)
    {
        var environment = FindEnvironment() ?? (JsonObject)(_root[EnvironmentKey] = new JsonObject());
        environment[name] = JsonValue.Create(value);
    }

    /// <inheritdoc />
    public string Serialize()
    {
        try
        {
            return Write(_root, Text);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            // A string with a lone surrogate escape parses but cannot be written back.
            throw new InvalidOperationException($"{Name} contains a string that is not valid Unicode text (a lone surrogate escape), so the tool cannot rewrite it. Fix that string first.", exception);
        }
    }

    private static string[] PathOf(SandboxSetting setting) => setting switch
    {
        SandboxSetting.AllowWrite => AllowWritePath,
        SandboxSetting.DenyWrite => DenyWritePath,
        SandboxSetting.DenyRead => DenyReadPath,
        SandboxSetting.AllowedDomains => DomainsPath,
        SandboxSetting.ExcludedCommands => ExcludedPath,
        SandboxSetting.AllowAllUnixSockets => SocketsPath,
        SandboxSetting.Environment => EnvironmentPath,
        _ => throw new ArgumentOutOfRangeException(nameof(setting), setting, "Not a sandbox setting."),
    };

    private static string JsonPathOf(string[] path) => "$." + string.Join('.', path);

    private JsonObject? FindEnvironment() => _root[EnvironmentKey] switch
    {
        null => null,
        JsonObject environment => environment,
        _ => throw new InvalidOperationException($"{Name}: {JsonPathOf(EnvironmentPath)} must be an object."),
    };

    // Walks to the object that holds the last segment; null when a segment is absent. A segment of another type is an error.
    private JsonObject? FindParent(string[] path)
    {
        var current = _root;
        for (var index = 0; index < path.Length - 1; index++)
        {
            if (!current.TryGetPropertyValue(path[index], out var child) || child is null)
            {
                return null;
            }

            current = child as JsonObject
                ?? throw new InvalidOperationException($"{Name}: {JsonPathOf(path[..(index + 1)])} must be an object.");
        }

        return current;
    }

    private JsonObject GetOrCreateParent(string[] path)
    {
        var current = _root;
        for (var index = 0; index < path.Length - 1; index++)
        {
            if (current[path[index]] is not JsonObject child)
            {
                child = [];
                current[path[index]] = child;
            }

            current = child;
        }

        return current;
    }

    private JsonArray? FindList(string[] path)
    {
        var parent = FindParent(path);
        if (parent is null || !parent.TryGetPropertyValue(path[^1], out var node) || node is null)
        {
            return null;
        }

        var list = node as JsonArray ?? throw new InvalidOperationException($"{Name}: {JsonPathOf(path)} must be a list of strings.");
        for (var index = 0; index < list.Count; index++)
        {
            if (!TryString(list[index], out _))
            {
                throw new InvalidOperationException(string.Create(CultureInfo.InvariantCulture, $"{Name}: {JsonPathOf(path)}[{index}] must be a string of valid Unicode text."));
            }
        }

        return list;
    }

    private JsonArray CreateList(string[] path)
    {
        var list = new JsonArray();
        GetOrCreateParent(path)[path[^1]] = list;
        return list;
    }

    // A string with a lone surrogate escape parses but cannot be read as text; it counts as not a string.
    private static bool TryString(JsonNode? node, out string? value)
    {
        value = null;
        try
        {
            return node is JsonValue json && json.TryGetValue(out value);
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static string Write(JsonObject root, string? existing)
    {
        var (indentCharacter, indentSize) = DetectIndent(existing);
        var newLine = existing is not null && existing.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var finalNewLine = existing is null || existing.EndsWith('\n');
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(
            stream,
            new JsonWriterOptions
            {
                Indented = true,
                IndentCharacter = indentCharacter,
                IndentSize = indentSize,
                NewLine = newLine,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            }))
        {
            root.WriteTo(writer);
        }

        var text = Encoding.UTF8.GetString(stream.GetBuffer(), 0, (int)stream.Length);
        return finalNewLine ? text + newLine : text;
    }

    private static (char Character, int Size) DetectIndent(string? text)
    {
        if (text is null)
        {
            return (' ', 2);
        }

        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.TrimStart(' ', '\t');
            var length = line.Length - trimmed.Length;
            if (length == 0 || trimmed.Length == 0 || trimmed[0] == '\r')
            {
                continue;
            }

            return line[0] == '\t' ? ('\t', 1) : (' ', Math.Clamp(length, 1, 8));
        }

        return (' ', 2);
    }
}
