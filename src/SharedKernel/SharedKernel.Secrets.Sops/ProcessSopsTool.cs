using System.Text;
using SharedKernel.Processes;
using SharedKernel.Results;

namespace SharedKernel.Secrets.Sops;

/// <summary>
/// <see cref="ISopsTool"/> over the <c>sops</c> program, started from an absolute path without a shell. Plaintext goes in on
/// standard input and out on standard output; arguments carry only public recipients, paths, and key paths; the only
/// environment variable is <c>SOPS_AGE_KEY_FILE</c>, set to the identity file's path. Every call passes <c>--config /dev/null</c> and starts in a new empty directory. Reading metadata does not start a process.
/// </summary>
public sealed class ProcessSopsTool : ISopsTool
{
    private const string AgeKeyFile = "SOPS_AGE_KEY_FILE";

    // The exit code of sops edit when the document was saved unchanged.
    private const int FileHasNotBeenModified = 200;

    // What sops prints (through age) when it read the file and the identity and the identity matches none of the recipients. Anything that
    // says the identity file, the header, or the file could not be read or parsed is not that verdict, even though sops uses the same first line.
    private const string NoMatchMarker = "identity did not match any of the recipients";
    private static readonly string[] BrokenRunMarkers = ["failed to load age identities", "failed to read header", "failed to parse", "Cannot decrypt MAC", "non-existent file"];

    private readonly IToolRunner _runner;
    private readonly string _sops;

    /// <summary>Creates the adapter.</summary>
    /// <param name="runner">Runs the program.</param>
    /// <param name="sopsPath">The absolute path of <c>sops</c>.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="sopsPath"/> is not absolute.</exception>
    public ProcessSopsTool(IToolRunner runner, string sopsPath)
    {
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(sopsPath);
        if (!Path.IsPathFullyQualified(sopsPath))
        {
            throw new ArgumentException("The tool path must be absolute.", nameof(sopsPath));
        }

        _runner = runner;
        _sops = sopsPath;
    }

    /// <summary>Creates the adapter over the <c>sops</c> that <paramref name="executableResolver"/> finds.</summary>
    /// <param name="runner">Runs the program, such as <see cref="SystemToolRunner"/>.</param>
    /// <param name="executableResolver">Finds the program, such as <see cref="PathExecutableResolver.Default"/>.</param>
    /// <returns>The adapter.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The program is not found.</exception>
    public static ProcessSopsTool Find(IToolRunner runner, IExecutableResolver executableResolver)
    {
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(executableResolver);
        return new ProcessSopsTool(runner, executableResolver.Find("sops") ?? throw new InvalidOperationException("sops was not found on PATH."));
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="recipients"/> is <see langword="null"/>.</exception>
    public async Task<Result<string>> Encrypt(SopsFormat format, AgeRecipientSet recipients, ReadOnlyMemory<byte> plaintext, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recipients);
        var type = FormatName(format);
        string[] arguments = ["encrypt", "--age", AgeList(recipients.Recipients), "--input-type", type, "--output-type", type, "/dev/stdin"];
        var completed = await RunSops(arguments, null, plaintext, cancellationToken).ConfigureAwait(false);
        if (completed.IsFailure)
        {
            return Result.Error<string>(completed.ErrorDetails?.Detail ?? "The program failed.");
        }

        var run = completed.Value;

        using (run)
        {
            return Result.Ok(Encoding.UTF8.GetString(run.StandardOutput.AsSpan()));
        }
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public Task<Result<SecretBytes>> Decrypt(string absoluteFile, AgeIdentityRef identity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(absoluteFile);
        ArgumentNullException.ThrowIfNull(identity);
        return RunForOutput(["decrypt", absoluteFile], absoluteFile, identity, cancellationToken);
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public Task<Result<SecretBytes>> DecryptValue(string root, SecretRef reference, AgeIdentityRef identity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(identity);
        var file = reference.File.ResolveUnder(root);
        return file.IsFailure
            ? Task.FromResult(Result.Invalid<SecretBytes>("The file of the reference cannot be resolved under the root.", "file", "The file of the reference cannot be resolved under the root."))
            : RunForOutput(["decrypt", "--extract", reference.SopsExtractPath, file.Value], file.Value, identity, cancellationToken);
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public async Task<SopsTrial> TrialDecrypt(string absoluteFile, AgeIdentityRef identity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(absoluteFile);
        ArgumentNullException.ThrowIfNull(identity);
        var invalid = ToolRuns.CheckFileArgument(absoluteFile);
        if (invalid is not null)
        {
            return new SopsTrial(SopsTrialOutcome.Failed, invalid);
        }

        var directory = Directory.CreateTempSubdirectory("sops-");
        try
        {
            ToolResult run;
            try
            {
                run = await _runner.Run(Request(["decrypt", absoluteFile], identity, default, directory.FullName), cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is InvalidOperationException or TimeoutException)
            {
                return new SopsTrial(SopsTrialOutcome.Failed, $"sops could not run: {exception.Message}");
            }

            using (run)
            {
                if (run.ExitCode == 0)
                {
                    return new SopsTrial(SopsTrialOutcome.Decrypted, string.Empty);
                }

                var matched = run.StandardError.Contains(NoMatchMarker, StringComparison.Ordinal)
                    && !BrokenRunMarkers.Any(marker => run.StandardError.Contains(marker, StringComparison.Ordinal));
                return new SopsTrial(matched ? SopsTrialOutcome.NoMatchingIdentity : SopsTrialOutcome.Failed, run.Describe("sops"));
            }
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public async Task<Result<string>> UpdateKeys(ReadOnlyMemory<byte> document, SopsFormat format, AgeIdentityRef identity, AgeRecipientSet recipients, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(recipients);

        // updatekeys rewrites the file it is given and takes the recipients only from a configuration file, so it gets a
        // private copy and a configuration that lists exactly the recipients and nothing else.
        var directory = Directory.CreateTempSubdirectory("sops-");
        try
        {
            var copy = await WritePrivateCopy(Directory.CreateDirectory(Path.Combine(directory.FullName, "data")).FullName, document, format, cancellationToken).ConfigureAwait(false);
            var configuration = Path.Combine(directory.FullName, ".sops.yaml");
            await File.WriteAllTextAsync(configuration, $"creation_rules:\n  - age: \"{AgeList(recipients.Recipients)}\"\n", cancellationToken).ConfigureAwait(false);
            var request = Request(["updatekeys", "--yes", "--input-type", FormatName(format), copy], identity, default, directory.FullName, configuration);
            var run = await _runner.RunToSuccess("sops", request, cancellationToken).ConfigureAwait(false);
            if (run.IsFailure)
            {
                return Result.Error<string>(run.ErrorDetails?.Detail ?? "sops failed.");
            }

            run.Value.Dispose();
            return Result.Ok(await File.ReadAllTextAsync(copy, cancellationToken).ConfigureAwait(false));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public async Task<Result<string>> Edit(ReadOnlyMemory<byte> document, SopsFormat format, AgeIdentityRef identity, SopsEditSession session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(session);
        if (!Path.IsPathFullyQualified(session.PrivateDirectory))
        {
            return Result.Invalid<string>("The private directory must be absolute.", "privateDirectory", "The private directory must be absolute.");
        }

        var data = Path.Combine(session.PrivateDirectory, "data");
        try
        {
            var copy = await WritePrivateCopy(Directory.CreateDirectory(data).FullName, document, format, cancellationToken).ConfigureAwait(false);
            Dictionary<string, string> environment = new(session.TerminalEnvironment, StringComparer.Ordinal)
            {
                ["HOME"] = session.PrivateDirectory,
                ["TMPDIR"] = session.PrivateDirectory,
                ["EDITOR"] = session.Editor.ToShellWords(),
                [AgeKeyFile] = identity.Path,
            };
            var type = FormatName(format);
            var request = new ToolRequest(_sops, ["--config", "/dev/null", "edit", "--input-type", type, "--output-type", type, copy], environment, default, session.PrivateDirectory);
            int exitCode;
            try
            {
                exitCode = await _runner.RunAttached(request, cancellationToken).ConfigureAwait(false);
            }
            catch (InvalidOperationException exception)
            {
                return Result.Error<string>($"sops could not run: {exception.Message}");
            }

            return exitCode is 0 or FileHasNotBeenModified
                ? Result.Ok(await File.ReadAllTextAsync(copy, cancellationToken).ConfigureAwait(false))
                : Result.Error<string>($"sops edit exited with code {exitCode}.");
        }
        finally
        {
            if (Directory.Exists(data))
            {
                Directory.Delete(data, recursive: true);
            }
        }
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public async Task<Result<string>> Rotate(ReadOnlyMemory<byte> document, SopsFormat format, AgeIdentityRef identity, AgeRecipientSet recipients, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(recipients);
        var current = SopsMetadataReader.Read(Encoding.UTF8.GetString(document.Span), format);
        if (current.IsFailure)
        {
            return Result.Error<string>(current.ErrorDetails?.Detail ?? "The recipients cannot be read.");
        }

        var type = FormatName(format);
        List<string> arguments = ["rotate", "--input-type", type, "--output-type", type];
        var added = recipients.Recipients.Where(recipient => !current.Value.Age.Contains(recipient)).ToArray();
        var removed = current.Value.Age.Where(recipient => !recipients.Contains(recipient)).ToArray();
        if (added.Length > 0)
        {
            arguments.AddRange(["--add-age", AgeList(added)]);
        }

        if (removed.Length > 0)
        {
            arguments.AddRange(["--rm-age", AgeList(removed)]);
        }

        // rotate writes the new version to standard output; the copy only gives sops a file to read.
        var directory = Directory.CreateTempSubdirectory("sops-");
        try
        {
            arguments.Add(await WritePrivateCopy(directory.FullName, document, format, cancellationToken).ConfigureAwait(false));
            var run = await _runner.RunToSuccess("sops", Request(arguments, identity, default, directory.FullName), cancellationToken).ConfigureAwait(false);
            if (run.IsFailure)
            {
                return Result.Error<string>(run.ErrorDetails?.Detail ?? "sops failed.");
            }

            using (run.Value)
            {
                return Result.Ok(Encoding.UTF8.GetString(run.Value.StandardOutput.AsSpan()));
            }
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="absoluteFile"/> is <see langword="null"/>.</exception>
    public Task<Result<SopsFileRecipients>> ListRecipients(string absoluteFile, CancellationToken cancellationToken) =>
        SopsFileReader.ReadRecipients(absoluteFile, cancellationToken);

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public async Task<Result<RecipientVerification>> VerifyRecipients(string absoluteFile, SopsCreationRule rule, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(rule);
        var actual = await ListRecipients(absoluteFile, cancellationToken).ConfigureAwait(false);
        return actual.IsFailure ? Result.Error<RecipientVerification>(actual.ErrorDetails?.Detail ?? "The recipients cannot be read.") : Result.Ok(RecipientVerification.Compare(actual.Value, rule));
    }

    private static string FormatName(SopsFormat format) => format == SopsFormat.Json ? "json" : "yaml";

    // The copy that sops reads and, for updatekeys and edit, rewrites: owner-only, so sops can write it whatever the
    // original's mode, and named with a lowercase extension, as sops expects.
    private static async Task<string> WritePrivateCopy(string directory, ReadOnlyMemory<byte> document, SopsFormat format, CancellationToken cancellationToken)
    {
        var copy = Path.Combine(directory, "document.sops." + FormatName(format));
        FileStreamOptions options = new() { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        var stream = new FileStream(copy, options);
        await using (stream.ConfigureAwait(false))
        {
            await stream.WriteAsync(document, cancellationToken).ConfigureAwait(false);
        }

        return copy;
    }

    private static string AgeList(IEnumerable<AgeRecipient> recipients) => string.Join(',', recipients.Select(static item => item.Value));

    /// <summary>
    /// Runs sops with <c>--config /dev/null</c> and in a new empty directory, so a <c>.sops.yaml</c> in the caller's working directory (a creation rule with
    /// <c>unencrypted_regex</c>, say) can neither change how data is encrypted nor leave plaintext in the result.
    /// </summary>
    private async Task<Result<ToolResult>> RunSops(IReadOnlyList<string> arguments, AgeIdentityRef? identity, ReadOnlyMemory<byte> standardInput, CancellationToken cancellationToken)
    {
        var directory = Directory.CreateTempSubdirectory("sops-");
        try
        {
            return await _runner.RunToSuccess("sops", Request(arguments, identity, standardInput, directory.FullName), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// The request for one sops call. <c>HOME</c> is the empty working directory, so sops cannot find SSH keys or a key file in the caller's home
    /// directory: only the identity that was given can decrypt.
    /// </summary>
    private ToolRequest Request(IReadOnlyList<string> arguments, AgeIdentityRef? identity, ReadOnlyMemory<byte> standardInput, string directory, string configuration = "/dev/null")
    {
        Dictionary<string, string> environment = new(StringComparer.Ordinal)
        {
            ["HOME"] = directory
        };
        if (identity is not null)
        {
            environment[AgeKeyFile] = identity.Path;
        }

        return new ToolRequest(_sops, ["--config", configuration, .. arguments], environment, standardInput, directory);
    }

    private async Task<Result<SecretBytes>> RunForOutput(IReadOnlyList<string> arguments, string absoluteFile, AgeIdentityRef identity, CancellationToken cancellationToken)
    {
        var invalid = ToolRuns.CheckFileArgument(absoluteFile);
        if (invalid is not null)
        {
            return Result.Invalid<SecretBytes>(invalid, "file", invalid);
        }

        var completed = await RunSops(arguments, identity, default, cancellationToken).ConfigureAwait(false);

        // The output moves out of the result; the caller now owns and disposes the bytes.
        return completed.IsFailure ? Result.Error<SecretBytes>(completed.ErrorDetails?.Detail ?? "The program failed.") : Result.Ok(completed.Value.StandardOutput);
    }
}
