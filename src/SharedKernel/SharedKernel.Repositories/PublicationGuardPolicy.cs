using System.Text.Json;
using SharedKernel.IO;

namespace SharedKernel.Repositories;

/// <summary>The public rules read from the staged publication policy.</summary>
/// <param name="NeverTrack">Git pathspecs that must select no staged entries.</param>
/// <param name="Patterns">Configured patterns by stable rule ID.</param>
/// <param name="AllowedPaths">Reasoned exact-path exceptions by rule ID.</param>
internal sealed record PublicationGuardPolicy(
    IReadOnlyList<string> NeverTrack,
    IReadOnlyDictionary<string, string> Patterns,
    IReadOnlyDictionary<string, HashSet<string>> AllowedPaths)
{
    private const string InvalidPolicy = "a valid staged publication policy is required";
    private const string InvalidAllowlist = "every allowlist entry requires a known rule, exact path, and reason";
    private static readonly JsonDocumentOptions DocumentOptions = new() { AllowDuplicateProperties = false };

    /// <summary>Reads and validates one staged policy blob.</summary>
    /// <exception cref="PublicationGuard.CheckFailure">The staged policy or its reasons are invalid.</exception>
    internal static PublicationGuardPolicy Read(byte[] data)
    {
        try
        {
            using var document = JsonDocument.Parse(RepositoryFile.WithoutUtf8ByteOrderMark(data), DocumentOptions);
            var root = document.RootElement;
            if (!HasFields(root, "version", "neverTrack", "patterns", "allowlist")
                || root.GetProperty("version").ValueKind != JsonValueKind.Number
                || !root.GetProperty("version").TryGetInt32(out var version)
                || version != 1)
            {
                throw new PublicationGuard.CheckFailure(InvalidPolicy);
            }

            var forbidden = ReadPaths(root.GetProperty("neverTrack"));
            var patterns = ReadPatterns(root.GetProperty("patterns"));
            if (forbidden.Count + patterns.Count == 0)
            {
                throw new PublicationGuard.CheckFailure(InvalidPolicy);
            }

            return new PublicationGuardPolicy(forbidden, patterns, ReadAllowlist(root.GetProperty("allowlist"), patterns));
        }
        catch (JsonException)
        {
            throw new PublicationGuard.CheckFailure(InvalidPolicy);
        }
    }

    private static bool IsLine(string? value) => !string.IsNullOrWhiteSpace(value) && !value.Any(char.IsControl);

    private static List<string> ReadPaths(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array)
        {
            throw new PublicationGuard.CheckFailure(InvalidPolicy);
        }

        return [.. value.EnumerateArray().Select(item => ReadLine(item, InvalidPolicy))];
    }

    private static Dictionary<string, string> ReadPatterns(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array)
        {
            throw new PublicationGuard.CheckFailure(InvalidPolicy);
        }

        Dictionary<string, string> patterns = new(StringComparer.Ordinal);
        foreach (var entry in value.EnumerateArray())
        {
            if (!HasFields(entry, "id", "regex"))
            {
                throw new PublicationGuard.CheckFailure(InvalidPolicy);
            }

            var id = ReadLine(entry.GetProperty("id"), InvalidPolicy);
            var pattern = ReadLine(entry.GetProperty("regex"), InvalidPolicy);
            if (!id.All(static character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_')
                || !patterns.TryAdd(id, pattern))
            {
                throw new PublicationGuard.CheckFailure(InvalidPolicy);
            }
        }

        return patterns;
    }

    private static Dictionary<string, HashSet<string>> ReadAllowlist(JsonElement value, Dictionary<string, string> patterns)
    {
        if (value.ValueKind != JsonValueKind.Array)
        {
            throw new PublicationGuard.CheckFailure(InvalidAllowlist);
        }

        Dictionary<string, HashSet<string>> allowlist = new(StringComparer.Ordinal);
        foreach (var entry in value.EnumerateArray())
        {
            if (!HasFields(entry, "rule", "path", "reason"))
            {
                throw new PublicationGuard.CheckFailure(InvalidAllowlist);
            }

            var id = ReadLine(entry.GetProperty("rule"), InvalidAllowlist);
            var path = ReadLine(entry.GetProperty("path"), InvalidAllowlist);
            _ = ReadLine(entry.GetProperty("reason"), InvalidAllowlist);
            if (!patterns.ContainsKey(id) || !RepositoryPath.IsValid(path))
            {
                throw new PublicationGuard.CheckFailure(InvalidAllowlist);
            }

            if (!allowlist.TryGetValue(id, out var paths))
            {
                paths = new HashSet<string>(StringComparer.Ordinal);
                allowlist.Add(id, paths);
            }

            paths.Add(path);
        }

        return allowlist;
    }

    private static bool HasFields(JsonElement value, params string[] fields) =>
        value.ValueKind == JsonValueKind.Object
        && value.EnumerateObject().Count() == fields.Length
        && fields.All(field => value.TryGetProperty(field, out _));

    private static string ReadLine(JsonElement value, string error) =>
        value.ValueKind == JsonValueKind.String && value.GetString() is { } line && IsLine(line)
            ? line
            : throw new PublicationGuard.CheckFailure(error);
}
