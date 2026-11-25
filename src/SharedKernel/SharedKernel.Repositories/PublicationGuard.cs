using System.Text;
using SharedKernel.IO;
using SharedKernel.Processes;

namespace SharedKernel.Repositories;

/// <summary>Checks the tracked Git index against a staged, consumer-owned publication policy.</summary>
public static class PublicationGuard
{
    /// <summary>The Git-index path of the public policy.</summary>
    internal const string PolicyPath = ".config/publication-guard.json";

    /// <summary>The working-tree path of the optional ignored expressions.</summary>
    internal const string LocalPatternsPath = ".config/publication-guard.local.patterns";

    private const int MaxPolicyBytes = 1024 * 1024;
    private const int MaxIndexBytes = 64 * 1024 * 1024;
    private const int MaxLinkBytes = 64 * 1024;
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>Writes a pass, explicit local-file skip, or bounded failure without disclosing matched content.</summary>
    /// <param name="rootPath">A directory inside the Git working tree.</param>
    /// <param name="output">Receives successful check and skip messages.</param>
    /// <param name="error">Receives a failure without pattern, path, or content values.</param>
    /// <param name="cancellationToken">Cancels the check and its Git child processes.</param>
    /// <returns>Zero for a passing check and one for a failed or incomplete check.</returns>
    /// <exception cref="ArgumentException"><paramref name="rootPath"/> is empty or whitespace.</exception>
    /// <exception cref="ArgumentNullException">An input argument is null.</exception>
    public static async Task<int> Run(string rootPath, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        try
        {
            var localSkip = await Check(rootPath, cancellationToken).ConfigureAwait(false);
            if (localSkip is not null)
            {
                await output.WriteLineAsync(localSkip.AsMemory(), cancellationToken).ConfigureAwait(false);
            }

            await output.WriteLineAsync("Publication guard passed.".AsMemory(), cancellationToken).ConfigureAwait(false);
            return 0;
        }
        catch (CheckFailure failure)
        {
            await error.WriteLineAsync(("Publication guard failed: " + failure.Message + ".").AsMemory(), cancellationToken)
                .ConfigureAwait(false);
            return 1;
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or TimeoutException
            or UnauthorizedAccessException or DecoderFallbackException)
        {
            await error.WriteLineAsync("Publication guard failed: could not complete the staged check.".AsMemory(), cancellationToken)
                .ConfigureAwait(false);
            return 1;
        }
    }

    private static async Task<string?> Check(string start, CancellationToken ct)
    {
        var rootResult = await Git(start, ["rev-parse", "--show-toplevel"], MaxLinkBytes, ct).ConfigureAwait(false);
        var root = StrictUtf8.GetString(RequireSuccess(rootResult, "the guard requires a Git work tree").Span).TrimEnd('\r', '\n');
        if (!Path.IsPathFullyQualified(root))
        {
            throw new CheckFailure("the guard requires a Git work tree");
        }

        var index = await ReadIndex(root, ct).ConfigureAwait(false);
        if (index.Any(static entry => entry.Path == LocalPatternsPath))
        {
            throw new CheckFailure("the local patterns file must not be tracked");
        }

        var policyEntry = index.SingleOrDefault(static entry => entry.Path == PolicyPath && entry.Mode is "100644" or "100755")
            ?? throw new CheckFailure("a valid staged publication policy is required");
        var policyBlob = await Git(root, ["cat-file", "blob", policyEntry.ObjectId], MaxPolicyBytes, ct).ConfigureAwait(false);
        var policy = PublicationGuardPolicy.Read(RequireSuccess(policyBlob, "a valid staged publication policy is required").ToArray());
        foreach (var pathspec in policy.NeverTrack)
        {
            var forbidden = await Git(root, ["ls-files", "--cached", "-z", "--", pathspec], MaxIndexBytes, ct).ConfigureAwait(false);
            if (!RequireSuccess(forbidden, "could not inspect a forbidden path rule").IsEmpty)
            {
                throw new CheckFailure("a forbidden path is staged");
            }
        }

        var local = await ReadLocalPatterns(root, ct).ConfigureAwait(false);
        if (policy.Patterns.Count != 0 || local.Patterns.Length != 0)
        {
            await Scan(root, policy, index, local.Patterns, ct).ConfigureAwait(false);
        }

        if (!index.SequenceEqual(await ReadIndex(root, ct).ConfigureAwait(false)))
        {
            throw new CheckFailure("the Git index changed while scanning");
        }

        return local.Skip;
    }

    private static async Task Scan(
        string root, PublicationGuardPolicy policy, IReadOnlyList<IndexEntry> index, string[] local, CancellationToken ct)
    {
        var scratch = Directory.CreateTempSubdirectory("sharedkernel-publication-");
        try
        {
            foreach (var (rule, pattern) in policy.Patterns)
            {
                var excluded = new HashSet<string>(StringComparer.Ordinal) { PolicyPath };
                if (policy.AllowedPaths.TryGetValue(rule, out var allowed))
                {
                    foreach (var path in allowed)
                    {
                        var directoryPrefix = path + "/";
                        if (index.Any(entry => entry.Path.StartsWith(directoryPrefix, StringComparison.Ordinal)))
                        {
                            throw new CheckFailure("an allowlist path must identify an exact staged file");
                        }

                        excluded.Add(path);
                    }
                }

                await ScanPatterns(root, scratch.FullName, index, [pattern], excluded, "configured", ct).ConfigureAwait(false);
            }

            if (local.Length > 0)
            {
                await ScanPatterns(root, scratch.FullName, index, local, [], "local", ct).ConfigureAwait(false);
            }
        }
        finally
        {
            scratch.Delete(recursive: true);
        }
    }

    private static async Task ScanPatterns(
        string root,
        string scratch,
        IReadOnlyList<IndexEntry> index,
        string[] patterns,
        HashSet<string> excluded,
        string scope,
        CancellationToken ct)
    {
        string[] options = ["--no-recurse-submodules", "--no-textconv", "-a", "-E", "-q", "-f", "-"];
        var input = Encoding.UTF8.GetBytes(string.Join('\n', patterns) + "\n");
        var staged = await Git(root, ["grep", "--cached", .. options, "--",
            .. excluded.Select(static path => ":(exclude,top,literal)" + path)], MaxLinkBytes, ct, input).ConfigureAwait(false);
        CheckGrep(staged, scope);

        const string fileName = "index-paths";
        var stream = new FileStream(Path.Combine(scratch, fileName), new FileStreamOptions
        {
            Mode = FileMode.Create,
            Access = FileAccess.Write,
            Options = FileOptions.Asynchronous,
        });
        await using (stream.ConfigureAwait(false))
        {
            foreach (var entry in index.Where(entry => !excluded.Contains(entry.Path)))
            {
                await stream.WriteAsync(Encoding.UTF8.GetBytes(entry.Path + "\n"), ct).ConfigureAwait(false);
                if (entry.Mode == "120000")
                {
                    var target = await Git(root, ["cat-file", "blob", entry.ObjectId], MaxLinkBytes, ct).ConfigureAwait(false);
                    await stream.WriteAsync(RequireSuccess(target, "could not inspect a staged symbolic link"), ct).ConfigureAwait(false);
                    await stream.WriteAsync("\n"u8.ToArray(), ct).ConfigureAwait(false);
                }
            }
        }

        var paths = await Git(scratch, ["grep", "--no-index", .. options, "--", fileName], MaxLinkBytes, ct, input)
            .ConfigureAwait(false);
        CheckGrep(paths, scope);
    }

    private static void CheckGrep(CapturedProcessOutput result, string scope)
    {
        if (result.StandardError.Length != 0 || result.ExitCode is not (0 or 1))
        {
            throw new CheckFailure($"could not scan a {scope} personal-data pattern");
        }

        if (result.ExitCode == 0)
        {
            throw new CheckFailure($"a {scope} personal-data pattern matched staged data");
        }
    }

    private static async Task<(string[] Patterns, string? Skip)> ReadLocalPatterns(string root, CancellationToken ct)
    {
        byte[]? content;
        try
        {
            var path = RepositoryPath.Parse(LocalPatternsPath).ResolveUnder(root);
            var attributes = File.GetAttributes(path);
            if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint | FileAttributes.Device)) != 0)
            {
                throw new CheckFailure("the optional local patterns file requires a regular file with no symbolic links");
            }

            content = await BoundedReader.ReadFile(path, MaxPolicyBytes, ct).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return ([], "skip: optional local patterns file is absent");
        }
        catch (InvalidOperationException)
        {
            throw new CheckFailure("the optional local patterns file requires a regular file with no symbolic links");
        }

        if (content is null)
        {
            throw new CheckFailure("the optional local patterns file exceeds 1 MiB");
        }

        var lines = StrictUtf8.GetString(RepositoryFile.WithoutUtf8ByteOrderMark(content).Span)
            .Split('\n').Select(static line => line.TrimEnd('\r')).ToArray();
        if (lines.Any(static line => line.Any(char.IsControl)))
        {
            throw new CheckFailure("could not read the optional local patterns file");
        }

        var patterns = lines.Where(static line => !string.IsNullOrWhiteSpace(line)
            && line.TrimStart()[0] != '#').ToArray();
        return patterns.Length == 0 ? ([], "skip: optional local patterns file is empty") : (patterns, null);
    }

    private static async Task<List<IndexEntry>> ReadIndex(string root, CancellationToken ct)
    {
        var result = await Git(root, ["ls-files", "--stage", "-z"], MaxIndexBytes, ct).ConfigureAwait(false);
        var data = StrictUtf8.GetString(RequireSuccess(result, "could not inspect staged paths").Span);
        List<IndexEntry> entries = [];
        foreach (var record in data.Split('\0', StringSplitOptions.RemoveEmptyEntries))
        {
            var split = record.IndexOf('\t', StringComparison.Ordinal);
            if (split < 0 || record[..split].Split(' ') is not [var mode, var objectId, var stage]
                || mode is not ("100644" or "100755" or "120000" or "160000")
                || objectId.Length is not (40 or 64) || !objectId.All(char.IsAsciiHexDigit))
            {
                throw new CheckFailure("could not inspect staged paths");
            }

            if (stage != "0")
            {
                throw new CheckFailure("resolve unmerged paths before checking publication");
            }

            entries.Add(new IndexEntry(record[(split + 1)..], mode, objectId));
        }

        return entries;
    }

    private static Task<CapturedProcessOutput> Git(
        string root, IReadOnlyList<string> arguments, int maxOutputBytes, CancellationToken ct, ReadOnlyMemory<byte> input = default)
    {
        var git = GitProcess.Default;
        var start = git.CreateStartInfo(root, ["--no-replace-objects", .. arguments]);
        start.Environment["GIT_NO_LAZY_FETCH"] = "1";
        start.Environment["LC_ALL"] = "C";
        return git.Run(start, maxOutputBytes, input, ct);
    }

    private static ReadOnlyMemory<byte> RequireSuccess(CapturedProcessOutput result, string error) =>
        result.ExitCode == 0 && result.StandardError.Length == 0
            ? result.StandardOutput
            : throw new CheckFailure(error);

    /// <summary>Stops a check with a diagnosed, content-free failure.</summary>
    internal sealed class CheckFailure : InvalidOperationException
    {
        /// <summary>Creates a generic failed check.</summary>
        public CheckFailure()
        {
        }

        /// <summary>Creates a failed check with its fixed diagnostic.</summary>
        public CheckFailure(string message) : base(message)
        {
        }

        /// <summary>Creates a failed check with an underlying failure.</summary>
        public CheckFailure(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    private sealed record IndexEntry(string Path, string Mode, string ObjectId);
}
