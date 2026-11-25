using System.Text;
using SharedKernel.Processes;

namespace SharedKernel.Repositories;

/// <summary>
/// Lists the files that the Git index of a repository tracks, through <see cref="GitProcess"/>.
/// </summary>
internal static class GitTrackedFiles
{
    private const int MaxOutputBytes = 64 * 1024 * 1024;
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    /// Returns the paths of the regular files in the Git index of <paramref name="rootPath"/> that match one of the
    /// pathspecs, relative to the root, separated by <c>/</c>, once each, and in index order. Symbolic links and
    /// submodules are left out.
    /// </summary>
    /// <exception cref="InvalidOperationException">Git cannot be found or started, or it cannot list the index.</exception>
    /// <exception cref="TimeoutException">Git does not finish within 30 seconds.</exception>
    public static Task<IReadOnlyList<string>> Read(string rootPath, IReadOnlyList<string> pathspecs, CancellationToken cancellationToken) =>
        Read(GitProcess.Default, rootPath, pathspecs, cancellationToken);

    /// <summary>Returns the tracked regular files that match one of the pathspecs, as <see cref="Read(string, IReadOnlyList{string}, CancellationToken)"/> does, running Git through <paramref name="git"/>.</summary>
    /// <exception cref="InvalidOperationException">Git cannot be found or started, or it cannot list the index.</exception>
    /// <exception cref="TimeoutException">Git does not finish within the time limit of <paramref name="git"/>.</exception>
    public static async Task<IReadOnlyList<string>> Read(GitProcess git, string rootPath, IReadOnlyList<string> pathspecs, CancellationToken cancellationToken)
    {
        List<string> paths = [];
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (var record in await ReadRecords(git, rootPath, ["ls-files", "-z", "--stage", "--", .. pathspecs], cancellationToken).ConfigureAwait(false))
        {
            // Each record is "<mode> <object> <stage>\t<path>"; an unmerged path has a record for each stage.
            var separator = record.IndexOf('\t', StringComparison.Ordinal);
            var mode = separator < 0 ? null : record.Split(' ', 2)[0];
            switch (mode)
            {
                case "100644" or "100755":
                    var path = record[(separator + 1)..];
                    if (seen.Add(path))
                    {
                        paths.Add(path);
                    }

                    break;
                case "120000" or "160000":
                    break;
                default:
                    throw Failure();
            }
        }

        return paths;
    }

    /// <summary>
    /// Returns the <c>git ls-files --eol</c> record of each entry in the Git index of <paramref name="rootPath"/>, in
    /// index order: <c>i/&lt;index&gt; w/&lt;working tree&gt; attr/&lt;attributes&gt;</c>, padded with spaces, then a tab
    /// and the path relative to the root.
    /// </summary>
    /// <exception cref="InvalidOperationException">Git cannot be found or started, or it cannot list the index.</exception>
    /// <exception cref="TimeoutException">Git does not finish within 30 seconds.</exception>
    public static Task<string[]> ReadLineEndings(string rootPath, CancellationToken cancellationToken) =>
        ReadRecords(GitProcess.Default, rootPath, ["ls-files", "-z", "--eol"], cancellationToken);

    private static async Task<string[]> ReadRecords(GitProcess git, string rootPath, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var startInfo = git.CreateStartInfo(rootPath, arguments);
        CapturedProcessOutput result;
        try
        {
            result = await git.Run(startInfo, MaxOutputBytes, ReadOnlyMemory<byte>.Empty, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException exception)
        {
            throw new TimeoutException($"Git index inspection timed out after {git.TimeoutSeconds} seconds.", exception);
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException(Failure().Message, exception);
        }

        if (result.ExitCode != 0)
        {
            throw Failure();
        }

        try
        {
            return StrictUtf8.GetString(result.StandardOutput.Span).Split('\0', StringSplitOptions.RemoveEmptyEntries);
        }
        catch (DecoderFallbackException)
        {
            throw Failure();
        }
    }

    private static InvalidOperationException Failure() => new("Git index inspection failed.");
}
