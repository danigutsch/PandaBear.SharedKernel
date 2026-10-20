using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SharedKernel.RepoConfig.Tool;

internal static class RoadmapGitChangeGuard
{
    private const int MaxRoadmapFileBytes = 4 * 1024 * 1024;
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public static RoadmapGitChangeSet Capture(string rootPath, byte[] nulDelimitedStatus)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        ArgumentNullException.ThrowIfNull(nulDelimitedStatus);

        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));
        var entries = ParseStatus(nulDelimitedStatus);
        foreach (var entry in entries)
        {
            ValidateModes(entry);
            ValidatePath(root, entry.Path, requireCurrentFileSafety: true);
            if (entry.OriginalPath is not null)
            {
                ValidatePath(root, entry.OriginalPath, requireCurrentFileSafety: false);
            }
        }

        var paths = entries
            .SelectMany(entry => entry.OriginalPath is null ? [entry.Path] : new[] { entry.Path, entry.OriginalPath })
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        return new RoadmapGitChangeSet(
            entries.Length > 0,
            ComputeDigest(root, entries),
            paths,
            entries);
    }

    public static void Verify(string rootPath, RoadmapGitChangeSet expected)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        ArgumentNullException.ThrowIfNull(expected);

        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));
        foreach (var entry in expected.Entries)
        {
            ValidateModes(entry);
            ValidatePath(root, entry.Path, requireCurrentFileSafety: true);
            if (entry.OriginalPath is not null)
            {
                ValidatePath(root, entry.OriginalPath, requireCurrentFileSafety: false);
            }
        }

        var digest = ComputeDigest(root, expected.Entries);
        if (!string.Equals(digest, expected.Digest, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Roadmap automation result changed during publication.");
        }
    }

    private static RoadmapGitStatusEntry[] ParseStatus(byte[] status)
    {
        if (status.Length == 0)
        {
            return [];
        }

        string value;
        try
        {
            value = StrictUtf8.GetString(status);
        }
        catch (DecoderFallbackException)
        {
            throw CreateParseException();
        }

        if (value[^1] != '\0')
        {
            throw CreateParseException();
        }

        var records = value.Split('\0');
        List<RoadmapGitStatusEntry> entries = [];
        var index = 0;
        while (index < records.Length - 1)
        {
            var record = records[index];
            index++;
            if (record.StartsWith("1 ", StringComparison.Ordinal))
            {
                var fields = record.Split(' ', 9, StringSplitOptions.None);
                if (fields.Length != 9 || string.IsNullOrEmpty(fields[8]))
                {
                    throw CreateParseException();
                }

                entries.Add(new RoadmapGitStatusEntry(
                    fields[0],
                    fields[1],
                    fields[2],
                    fields[3],
                    fields[4],
                    fields[5],
                    fields[8],
                    OriginalPath: null));
                continue;
            }

            if (record.StartsWith("2 ", StringComparison.Ordinal))
            {
                var fields = record.Split(' ', 10, StringSplitOptions.None);
                if (fields.Length != 10
                    || string.IsNullOrEmpty(fields[9])
                    || index >= records.Length - 1
                    || string.IsNullOrEmpty(records[index]))
                {
                    throw CreateParseException();
                }

                if (fields[8].StartsWith('C'))
                {
                    throw new InvalidOperationException("Roadmap automation rejects Git copy changes.");
                }

                if (fields[8].Length < 2
                    || fields[8][0] != 'R'
                    || fields[8][1..].Any(character => character is < '0' or > '9'))
                {
                    throw CreateParseException();
                }

                entries.Add(new RoadmapGitStatusEntry(
                    fields[0],
                    fields[1],
                    fields[2],
                    fields[3],
                    fields[4],
                    fields[5],
                    fields[9],
                    records[index]));
                index++;
                continue;
            }

            if (record.StartsWith("? ", StringComparison.Ordinal) && record.Length > 2)
            {
                entries.Add(new RoadmapGitStatusEntry(
                    "?",
                    "??",
                    "N...",
                    "000000",
                    "000000",
                    "100644",
                    record[2..],
                    OriginalPath: null));
                continue;
            }

            throw CreateParseException();
        }

        return entries.ToArray();
    }

    private static void ValidateModes(RoadmapGitStatusEntry entry)
    {
        var modes = new[] { entry.HeadMode, entry.IndexMode, entry.WorkTreeMode };
        if (modes.Contains("120000", StringComparer.Ordinal))
        {
            throw new InvalidOperationException("Roadmap automation rejects symlink changes.");
        }

        if (modes.Contains("160000", StringComparer.Ordinal)
            || !string.Equals(entry.Submodule, "N...", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Roadmap automation rejects submodule changes.");
        }
    }

    private static void ValidatePath(string root, string path, bool requireCurrentFileSafety)
    {
        if (string.IsNullOrEmpty(path)
            || Path.IsPathRooted(path)
            || path[0] is '/' or '\\'
            || path.Contains('\\', StringComparison.Ordinal)
            || (path.Length >= 3 && char.IsAsciiLetter(path[0]) && path[1] == ':' && path[2] is '/' or '\\')
            || path.Any(char.IsControl))
        {
            throw new InvalidOperationException("Roadmap automation detected an unsafe repository path.");
        }

        var normalized = path;
        var segments = normalized.Split('/');
        if (segments.Any(segment => segment is "" or "." or ".."))
        {
            throw new InvalidOperationException("Roadmap automation detected an unsafe repository path.");
        }

        if (!normalized.StartsWith("roadmap/", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Roadmap automation detected a change outside roadmap/**.");
        }

        var fullPath = Path.GetFullPath(Path.Combine(root, normalized.Replace('/', Path.DirectorySeparatorChar)));
        var relative = Path.GetRelativePath(root, fullPath).Replace(Path.DirectorySeparatorChar, '/');
        if (!string.Equals(relative, normalized, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Roadmap automation detected an unsafe repository path.");
        }

        if (requireCurrentFileSafety)
        {
            ValidateCurrentFile(root, fullPath);
        }
    }

    private static void ValidateCurrentFile(string root, string fullPath)
    {
        var current = new DirectoryInfo(root);
        var relative = Path.GetRelativePath(root, fullPath);
        foreach (var segment in relative.Split(Path.DirectorySeparatorChar))
        {
            var candidate = Path.Combine(current.FullName, segment);
            if (Directory.Exists(candidate))
            {
                var directory = new DirectoryInfo(candidate);
                RejectLink(directory);
                current = directory;
                continue;
            }

            if (File.Exists(candidate) || new FileInfo(candidate).LinkTarget is not null)
            {
                RejectLink(new FileInfo(candidate));
            }
        }

        if (Directory.Exists(fullPath))
        {
            throw new InvalidOperationException("Roadmap automation accepts changed files only.");
        }
    }

    private static void RejectLink(FileSystemInfo info)
    {
        if (info.LinkTarget is not null || info.Attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            throw new InvalidOperationException("Roadmap automation rejects symlink changes.");
        }
    }

    private static string ComputeDigest(string root, IEnumerable<RoadmapGitStatusEntry> entries)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartArray();
            foreach (var entry in entries
                         .OrderBy(entry => entry.Path, StringComparer.Ordinal)
                         .ThenBy(entry => entry.OriginalPath, StringComparer.Ordinal))
            {
                writer.WriteStartObject();
                writer.WriteString("recordType", entry.RecordType);
                writer.WriteString("status", entry.Status);
                writer.WriteString("submodule", entry.Submodule);
                writer.WriteString("headMode", entry.HeadMode);
                writer.WriteString("indexMode", entry.IndexMode);
                writer.WriteString("workTreeMode", entry.WorkTreeMode);
                writer.WriteString("path", entry.Path);
                if (entry.OriginalPath is not null)
                {
                    writer.WriteString("originalPath", entry.OriginalPath);
                }

                var fullPath = Path.GetFullPath(Path.Combine(root, entry.Path.Replace('/', Path.DirectorySeparatorChar)));
                if (File.Exists(fullPath))
                {
                    var file = new FileInfo(fullPath);
                    if (file.Length > MaxRoadmapFileBytes)
                    {
                        throw new InvalidOperationException("Roadmap automation changed file exceeds the allowed size.");
                    }

                    writer.WriteNumber("length", file.Length);
                    using var content = file.OpenRead();
                    writer.WriteString("sha256", Convert.ToHexStringLower(SHA256.HashData(content)));
                }
                else
                {
                    writer.WriteNull("sha256");
                }

                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        return Convert.ToHexStringLower(SHA256.HashData(stream.ToArray()));
    }

    private static InvalidOperationException CreateParseException() =>
        new("Roadmap automation could not parse NUL-delimited Git status.");
}
