using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Unicode;

namespace PandaBear.RepoConfig.Tool;

/// <summary>
/// Verifies the working-tree content of the JSON files that the Git index tracks.
/// </summary>
internal static class JsonVerifier
{
    /// <summary>
    /// The largest tracked JSON file, in bytes, that the tool reads.
    /// </summary>
    public const int MaxFileBytes = 64 * 1024 * 1024;

    // Editor and Dev Container settings are JSON with comments and trailing commas.
    private static readonly JsonReaderOptions ReaderOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    /// <summary>
    /// Reports each tracked <c>*.json</c> file whose working-tree content is missing, larger than
    /// <see cref="MaxFileBytes"/>, not UTF-8, or not JSON. A UTF-8 byte order mark, comments, and trailing commas are
    /// accepted.
    /// </summary>
    /// <exception cref="InvalidOperationException">Git cannot list the index.</exception>
    /// <exception cref="TimeoutException">Git does not list the index in time.</exception>
    public static async Task<IReadOnlyList<RepoConfigIssue>> Verify(string rootPath, CancellationToken cancellationToken)
    {
        var root = Path.GetFullPath(rootPath);
        var paths = await GitTrackedFiles.Read(root, ["*.json"], cancellationToken).ConfigureAwait(false);
        List<RepoConfigIssue> issues = [];
        foreach (var path in paths)
        {
            var (content, failure) = await ReadTrackedFile(root, path, cancellationToken).ConfigureAwait(false);
            failure ??= Validate(RepositoryFile.WithoutUtf8ByteOrderMark(content!).Span);
            if (failure is not null)
            {
                issues.Add(new RepoConfigIssue(path, failure));
            }
        }

        return issues;
    }

    /// <summary>
    /// Reads the working-tree content of a tracked file, or describes why it cannot be read.
    /// </summary>
    internal static async Task<(byte[]? Content, string? Failure)> ReadTrackedFile(string root, string path, CancellationToken cancellationToken)
    {
        try
        {
            var content = await RepositoryFile.Read(Path.Combine(root, path), MaxFileBytes, cancellationToken).ConfigureAwait(false);
            return content is null ? (null, "Exceeds the 64 MiB JSON size limit.") : (content, null);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return (null, "Is missing from the working tree.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return (null, "Cannot be read.");
        }
    }

    /// <summary>
    /// Describes a JSON syntax error with its one-based line and byte position in the line.
    /// </summary>
    internal static string DescribeInvalidJson(JsonException exception)
    {
        // The reader ends its message with the zero-based location, which this description gives one-based instead.
        var message = exception.Message;
        var location = message.IndexOf(" LineNumber: ", StringComparison.Ordinal);
        var description = location < 0 ? message : message[..location];
        return exception.LineNumber is { } line
            ? string.Create(CultureInfo.InvariantCulture, $"Invalid JSON at line {line + 1}, byte {(exception.BytePositionInLine ?? 0) + 1}: {description}")
            : "Invalid JSON: " + description;
    }

    /// <summary>
    /// Describes the one-based line and byte position in the line of the first invalid UTF-8 sequence.
    /// </summary>
    internal static string DescribeInvalidUtf8(ReadOnlySpan<byte> content)
    {
        var offset = 0;
        while (Rune.DecodeFromUtf8(content[offset..], out _, out var consumed) == OperationStatus.Done)
        {
            offset += consumed;
        }

        var preceding = content[..offset];
        var line = preceding.Count((byte)'\n') + 1;
        var position = offset - preceding.LastIndexOf((byte)'\n');
        return string.Create(CultureInfo.InvariantCulture, $"Invalid UTF-8 at line {line}, byte {position}.");
    }

    private static string? Validate(ReadOnlySpan<byte> content)
    {
        if (!Utf8.IsValid(content))
        {
            return DescribeInvalidUtf8(content);
        }

        try
        {
            var reader = new Utf8JsonReader(content, ReaderOptions);
            while (reader.Read())
            {
            }

            return null;
        }
        catch (JsonException exception)
        {
            return DescribeInvalidJson(exception);
        }
    }
}
