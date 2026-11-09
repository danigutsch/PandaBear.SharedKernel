using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PandaBear.Sandbox.Tool;

/// <summary>
/// Merges the desired entries into the text of a settings file. Only the entries the tool is asked for are added (or, for
/// <c>denyRead</c> with a risk flag, removed); every other key and every other list entry stays as it is. Nothing is ever
/// removed because it is absent from the configuration.
/// </summary>
internal static class SettingsMerger
{
    /// <summary>Compares paths ignoring a trailing slash.</summary>
    internal static readonly IEqualityComparer<string> PathComparer = new TrailingSlashComparer();
    private static readonly string[] AllowWritePath = ["sandbox", "filesystem", "allowWrite"];
    private static readonly string[] DenyWritePath = ["sandbox", "filesystem", "denyWrite"];
    private static readonly string[] DenyReadPath = ["sandbox", "filesystem", "denyRead"];
    private static readonly string[] DomainsPath = ["sandbox", "network", "allowedDomains"];
    private static readonly string[] ExcludedPath = ["sandbox", "excludedCommands"];
    private static readonly string[] SocketsPath = ["sandbox", "network", "allowAllUnixSockets"];

    /// <summary>Compares the file text with <paramref name="desired"/> and returns the edits and the resulting text.</summary>
    /// <param name="desired">The entries the settings file should contain.</param>
    /// <param name="existing">The current file text, or <see langword="null"/> when the file does not exist.</param>
    /// <param name="file">The file name used in error messages.</param>
    /// <exception cref="InvalidOperationException">The text is not a valid settings file, or a managed key has the wrong type. The message names the file and the JSON path.</exception>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static SettingsPlan Plan(DesiredSettings desired, string? existing, string file)
    {
        ArgumentNullException.ThrowIfNull(desired);
        ArgumentNullException.ThrowIfNull(file);

        var root = Parse(existing, file);
        List<SettingsChange> changes = [];
        List<string> warnings = [];

        AddMissing(root, AllowWritePath, desired.AllowWrite, PathComparer, changes, file);
        AddMissing(root, DenyWritePath, desired.DenyWrite, PathComparer, changes, file);
        AddMissing(root, DenyReadPath, desired.DenyRead, PathComparer, changes, file);
        AddMissing(root, DomainsPath, desired.AllowedDomains, StringComparer.OrdinalIgnoreCase, changes, file);
        AddMissing(root, ExcludedPath, desired.ExcludedCommands, StringComparer.Ordinal, changes, file);
        RemoveEntries(root, DenyReadPath, desired.RemoveDenyRead, changes, file);
        SetSockets(root, desired.AllowAllUnixSockets, changes, warnings, file);
        MergeEnvironment(root, desired.Environment, changes, warnings, file);

        if (changes.Count == 0)
        {
            return new SettingsPlan(changes, warnings, existing ?? string.Empty, existing);
        }

        try
        {
            return new SettingsPlan(changes, warnings, Serialize(root, existing), existing);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            // A string with a lone surrogate escape parses but cannot be written back.
            throw new InvalidOperationException($"{file} contains a string that is not valid Unicode text (a lone surrogate escape), so the tool cannot rewrite it. Fix that string first.", exception);
        }
    }

    private static JsonObject Parse(string? text, string file)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        try
        {
            var node = JsonNode.Parse(
                text,
                nodeOptions: null,
                new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Disallow, AllowTrailingCommas = false, AllowDuplicateProperties = false });
            return node as JsonObject
                ?? throw new InvalidOperationException($"{file}: $ must be a JSON object.");
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{file} is not valid JSON at {exception.Path ?? "$"}. Check for a repeated property, a comment, a trailing comma, or broken syntax."),
                exception);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidOperationException($"{file} is not valid JSON: a property is repeated or the nesting is too deep.", exception);
        }
    }

    private static string JsonPathOf(string[] path) => "$." + string.Join('.', path);

    // Walks to the object that holds the last segment; null when a segment is absent. A segment of another type is an error.
    private static JsonObject? FindParent(JsonObject root, string[] path, string file)
    {
        var current = root;
        for (var index = 0; index < path.Length - 1; index++)
        {
            if (!current.TryGetPropertyValue(path[index], out var child) || child is null)
            {
                return null;
            }

            current = child as JsonObject
                ?? throw new InvalidOperationException($"{file}: {JsonPathOf(path[..(index + 1)])} must be an object.");
        }

        return current;
    }

    private static JsonObject GetOrCreateParent(JsonObject root, string[] path)
    {
        var current = root;
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

    private static JsonArray? FindList(JsonObject root, string[] path, string file)
    {
        var parent = FindParent(root, path, file);
        if (parent is null || !parent.TryGetPropertyValue(path[^1], out var node) || node is null)
        {
            return null;
        }

        var list = node as JsonArray ?? throw new InvalidOperationException($"{file}: {JsonPathOf(path)} must be a list of strings.");
        for (var index = 0; index < list.Count; index++)
        {
            if (!TryString(list[index], out _))
            {
                throw new InvalidOperationException(string.Create(CultureInfo.InvariantCulture, $"{file}: {JsonPathOf(path)}[{index}] must be a string of valid Unicode text."));
            }
        }

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

    private static HashSet<string> Entries(JsonArray? list, IEqualityComparer<string> comparer) =>
        new((list ?? []).Select(static node => node!.GetValue<string>()), comparer);

    private static void AddMissing(JsonObject root, string[] path, IReadOnlyList<string> wanted, IEqualityComparer<string> comparer, List<SettingsChange> changes, string file)
    {
        var list = FindList(root, path, file);
        var present = Entries(list, comparer);
        foreach (var entry in wanted)
        {
            if (!present.Add(entry))
            {
                continue;
            }

            list ??= CreateList(root, path);
            list.Add((JsonNode?)JsonValue.Create(entry));
            changes.Add(new SettingsChange(SettingsChangeKind.Add, JsonPathOf(path), entry));
        }
    }

    private static JsonArray CreateList(JsonObject root, string[] path)
    {
        var list = new JsonArray();
        GetOrCreateParent(root, path)[path[^1]] = list;
        return list;
    }

    private static void RemoveEntries(JsonObject root, string[] path, IReadOnlyList<string> unwanted, List<SettingsChange> changes, string file)
    {
        var list = FindList(root, path, file);
        if (list is null)
        {
            return;
        }

        foreach (var entry in unwanted)
        {
            for (var index = list.Count - 1; index >= 0; index--)
            {
                if (PathComparer.Equals(list[index]!.GetValue<string>(), entry))
                {
                    list.RemoveAt(index);
                    changes.Add(new SettingsChange(SettingsChangeKind.Remove, JsonPathOf(path), entry));
                }
            }
        }
    }

    private static void SetSockets(JsonObject root, bool wanted, List<SettingsChange> changes, List<string> warnings, string file)
    {
        var parent = FindParent(root, SocketsPath, file);
        JsonNode? node = null;
        _ = parent?.TryGetPropertyValue(SocketsPath[^1], out node);
        var enabled = false;
        if (node is not null && !(node is JsonValue value && value.TryGetValue(out enabled)))
        {
            // Never replace a value of another type silently.
            if (wanted)
            {
                throw new InvalidOperationException($"{file}: {JsonPathOf(SocketsPath)} must be a boolean.");
            }

            warnings.Add($"{JsonPathOf(SocketsPath)} is not a boolean; the tool leaves it as it is.");
            return;
        }

        if (wanted && !enabled)
        {
            GetOrCreateParent(root, SocketsPath)[SocketsPath[^1]] = JsonValue.Create(true);
            changes.Add(new SettingsChange(SettingsChangeKind.Set, JsonPathOf(SocketsPath), "true"));
        }
        else if (!wanted && enabled)
        {
            warnings.Add($"{JsonPathOf(SocketsPath)} is true in the file; the tool does not manage it and leaves it as it is.");
        }
    }

    private static void MergeEnvironment(JsonObject root, IReadOnlyDictionary<string, string> wanted, List<SettingsChange> changes, List<string> warnings, string file)
    {
        if (wanted.Count == 0)
        {
            return;
        }

        var existing = root["env"] switch
        {
            null => null,
            JsonObject environment => environment,
            _ => throw new InvalidOperationException($"{file}: $.env must be an object."),
        };
        foreach (var (name, value) in wanted)
        {
            if (existing is not null && existing.TryGetPropertyValue(name, out var current) && current is not null)
            {
                if (!(TryString(current, out var currentValue) && string.Equals(currentValue, value, StringComparison.Ordinal)))
                {
                    warnings.Add($"$.env.{name} has a different value; the tool leaves it as it is.");
                }

                continue;
            }

            existing ??= (JsonObject)(root["env"] = new JsonObject());
            existing[name] = JsonValue.Create(value);
            changes.Add(new SettingsChange(SettingsChangeKind.Add, "$.env", name));
        }
    }

    private static string Serialize(JsonObject root, string? existing)
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

    private sealed class TrailingSlashComparer : IEqualityComparer<string>
    {
        public bool Equals(string? x, string? y) => string.Equals(Normalize(x), Normalize(y), StringComparison.Ordinal);

        public int GetHashCode(string obj) => Normalize(obj).GetHashCode(StringComparison.Ordinal);

        private static string Normalize(string? value) => value is { Length: > 1 } ? value.TrimEnd('/') is { Length: > 0 } trimmed ? trimmed : "/" : value ?? string.Empty;
    }
}
