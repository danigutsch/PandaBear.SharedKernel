using System.Text;
using System.Text.RegularExpressions;
using SharedKernel.InputNormalization;
using SharedKernel.IO;

namespace SharedKernel.Repositories;

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
        foreach (var (file, executable) in baseline.ManagedFiles)
        {
            var fullPath = Resolve(root, file.Path, file.Path);
            var template = file.Profile is { } profile
                ? (BaselineProfiles.TryGet(profile, out var embedded) ? embedded : string.Empty)
                : await ReadSource(root, file, cancellationToken).ConfigureAwait(false);
            var body = Substitute(file.Path, template, baseline).Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n') + "\n";
            var desired = BaselineMarker.Compose(file.Path, body);
            var existing = ReadExisting(fullPath, file.Path);
            var status = Compare(file.Path, desired, existing);
            if (status == BaselineStatus.Current && executable && !OperatingSystem.IsWindows()
                && !File.GetUnixFileMode(fullPath).HasFlag(UnixFileMode.UserExecute))
            {
                status = BaselineStatus.Outdated;
            }

            entries.Add(new BaselineEntry(file.Path, fullPath, status, desired, existing, file.OverwriteExisting, executable));
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
            return RepositoryPath.Parse(relativePath).ResolveUnder(root);
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException($"{owner}: {exception.Message}", exception);
        }
    }

    private static Task<string> ReadSource(string root, BaselineFileConfiguration file, CancellationToken cancellationToken)
    {
        var source = file.Source!;
        return ReadSourceText(Resolve(root, source, file.Path), file.Path, source, cancellationToken);
    }

    /// <summary>Reads a bounded UTF-8 source and identifies its destination in validation errors.</summary>
    /// <exception cref="InvalidOperationException">The named source is missing, too large, or invalid UTF-8.</exception>
    internal static async Task<string> ReadSourceText(string path, string destination, string source, CancellationToken cancellationToken)
    {
        var displaySource = ControlCharacters.Escape(source);
        try
        {
            var bytes = await BoundedReader.ReadFile(path, MaxBytes, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"{destination}: source {displaySource} exceeds the 1 MiB limit.");
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
                .GetString(RepositoryFile.WithoutUtf8ByteOrderMark(bytes).Span);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            throw new InvalidOperationException($"{destination}: source {displaySource} does not exist.", exception);
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidOperationException($"{destination}: source {displaySource} is not valid UTF-8.", exception);
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

    /// <summary>Renders baseline variables and, for caller-supplied policy files, caller-provided values.</summary>
    /// <exception cref="InvalidOperationException">The template names an unknown or missing variable.</exception>
    internal static string Substitute(
        string path,
        string template,
        BaselineConfiguration baseline,
        RepositoryPolicySources? policySources = null) =>
        VariablePattern().Replace(template, match => match.Groups[1].Value switch
        {
            "repositoryName" => baseline.RepositoryName,
            "defaultBranch" => baseline.DefaultBranch,
            "owner" when policySources is not null => RequirePolicyVariable(path, match.Value, policySources.Owner, "--owner"),
            "copyrightHolder" when policySources is not null => RequirePolicyVariable(path, match.Value, policySources.CopyrightHolder, "--copyright-holder"),
            _ when policySources is not null => throw new InvalidOperationException(
                $"{path}: unknown variable {match.Value}; the variables are @@repositoryName@@, @@defaultBranch@@, @@owner@@, and @@copyrightHolder@@."),
            _ => throw new InvalidOperationException($"{path}: unknown variable {match.Value}; the variables are @@repositoryName@@ and @@defaultBranch@@."),
        });

    private static string RequirePolicyVariable(string path, string placeholder, string? value, string option) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"{path}: {placeholder} requires {option}.")
            : value;

    [GeneratedRegex("@@([A-Za-z]+)@@", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex VariablePattern();
}
