using System.Text;
using System.Text.RegularExpressions;
using PandaBear.Tooling;

namespace PandaBear.RepoConfig.Tool;

/// <summary>Renders the desired content of each managed file and compares it with the working tree.</summary>
internal static partial class BaselinePlanner
{
    private const int MaxBytes = 1024 * 1024;

    /// <summary>Plans every configured file under <paramref name="root"/> without following symbolic links.</summary>
    /// <exception cref="InvalidOperationException">A source is missing or too large, a variable is unknown, or a path is unusable.</exception>
    public static async Task<IReadOnlyList<BaselineEntry>> Plan(string root, BaselineConfiguration baseline, CancellationToken cancellationToken)
    {
        root = Path.GetFullPath(root);
        List<BaselineEntry> entries = [];
        foreach (var file in baseline.Files)
        {
            var fullPath = Resolve(root, file.Path, file.Path);
            var template = file.Profile is { } profile
                ? (BaselineProfiles.TryGet(profile, out var embedded) ? embedded : string.Empty)
                : await ReadSource(root, file, cancellationToken).ConfigureAwait(false);
            var body = Substitute(file.Path, template, baseline).Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n') + "\n";
            var desired = BaselineMarker.Compose(file.Path, body);
            var existing = ReadExisting(fullPath, file.Path);
            entries.Add(new BaselineEntry(file.Path, fullPath, Compare(file.Path, desired, existing), desired, existing, file.OverwriteExisting));
        }

        return entries;
    }

    private static BaselineStatus Compare(string path, string desired, string? existing)
    {
        if (existing is null)
        {
            return BaselineStatus.Missing;
        }

        // Line endings are not drift: a checkout may convert them.
        var text = existing.Replace("\r\n", "\n", StringComparison.Ordinal);
        if (text == desired)
        {
            return BaselineStatus.Current;
        }

        if (!BaselineMarker.TryRead(path, text, out var hash, out var body))
        {
            return BaselineStatus.Unmanaged;
        }

        return BaselineMarker.Hash(body) == hash ? BaselineStatus.Outdated : BaselineStatus.Modified;
    }

    private static string Resolve(string root, string relativePath, string owner)
    {
        try
        {
            return RepositoryPath.Resolve(root, relativePath);
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException($"{owner}: {exception.Message}", exception);
        }
    }

    private static async Task<string> ReadSource(string root, BaselineFileConfiguration file, CancellationToken cancellationToken)
    {
        var source = file.Source!;
        var path = Resolve(root, source, file.Path);
        try
        {
            var bytes = await RepositoryFile.Read(path, MaxBytes, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"{file.Path}: source {source} exceeds the 1 MiB limit.");
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
                .GetString(RepositoryFile.WithoutUtf8ByteOrderMark(bytes).Span);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            throw new InvalidOperationException($"{file.Path}: source {source} does not exist.", exception);
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidOperationException($"{file.Path}: source {source} is not valid UTF-8.", exception);
        }
    }

    private static string? ReadExisting(string fullPath, string path)
    {
        if (Directory.Exists(fullPath))
        {
            throw new InvalidOperationException($"{path}: a directory exists at this path.");
        }

        var info = new FileInfo(fullPath);
        if (!info.Exists)
        {
            return null;
        }

        if (info.Length > MaxBytes)
        {
            throw new InvalidOperationException($"{path}: the existing file exceeds the 1 MiB limit.");
        }

        // The write batch compares with File.ReadAllText, so the same call supplies the expected content.
        return File.ReadAllText(fullPath);
    }

    private static string Substitute(string path, string template, BaselineConfiguration baseline) =>
        VariablePattern().Replace(template, match => match.Groups[1].Value switch
        {
            "repositoryName" => baseline.RepositoryName,
            "defaultBranch" => baseline.DefaultBranch,
            _ => throw new InvalidOperationException($"{path}: unknown variable {match.Value}; the variables are @@repositoryName@@ and @@defaultBranch@@."),
        });

    [GeneratedRegex("@@([A-Za-z]+)@@", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex VariablePattern();
}
