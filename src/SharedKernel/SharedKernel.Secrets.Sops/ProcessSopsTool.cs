using System.Text;
using PandaBear.Processes;
using PandaBear.Results;

namespace PandaBear.Secrets.Sops;

/// <summary>
/// <see cref="ISopsTool"/> over the <c>sops</c> program, started from an absolute path without a shell. Plaintext goes in on
/// standard input and out on standard output; arguments carry only public recipients, paths, and key paths; the only
/// environment variable is <c>SOPS_AGE_KEY_FILE</c>, set to the identity file's path. Every call passes <c>--config /dev/null</c> and starts in a new empty directory. Reading metadata does not start a process.
/// </summary>
public sealed class ProcessSopsTool : ISopsTool
{
    private const string AgeKeyFile = "SOPS_AGE_KEY_FILE";

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
        string[] arguments = ["encrypt", "--age", string.Join(',', recipients.Recipients.Select(static item => item.Value)), "--input-type", type, "--output-type", type, "/dev/stdin"];
        var (run, problem) = await RunSops(arguments, null, plaintext, cancellationToken).ConfigureAwait(false);
        if (run is null)
        {
            return ProgramInvocation.Error<string>(problem!);
        }

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
        var invalid = ProgramInvocation.CheckFile(absoluteFile);
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
    /// <exception cref="ArgumentNullException"><paramref name="absoluteFile"/> is <see langword="null"/>.</exception>
    public Task<Result<SopsFileRecipients>> ListRecipients(string absoluteFile, CancellationToken cancellationToken) =>
        SopsFileReader.ReadRecipients(absoluteFile, cancellationToken);

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public async Task<Result<RecipientVerification>> VerifyRecipients(string absoluteFile, SopsCreationRule rule, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(rule);
        var actual = await ListRecipients(absoluteFile, cancellationToken).ConfigureAwait(false);
        return actual.IsFailure ? ProgramInvocation.Error<RecipientVerification>(actual.ErrorDetails?.Detail ?? "The recipients cannot be read.") : Result.Ok(RecipientVerification.Compare(actual.Value, rule));
    }

    private static string FormatName(SopsFormat format) => format == SopsFormat.Json ? "json" : "yaml";

    /// <summary>
    /// Runs sops with <c>--config /dev/null</c> and in a new empty directory, so a <c>.sops.yaml</c> in the caller's working directory (a creation rule with
    /// <c>unencrypted_regex</c>, say) can neither change how data is encrypted nor leave plaintext in the result.
    /// </summary>
    private async Task<(ToolResult? Run, string? Problem)> RunSops(IReadOnlyList<string> arguments, AgeIdentityRef? identity, ReadOnlyMemory<byte> standardInput, CancellationToken cancellationToken)
    {
        var directory = Directory.CreateTempSubdirectory("sops-");
        try
        {
            return await ProgramInvocation.Run(_runner, "sops", Request(arguments, identity, standardInput, directory.FullName), cancellationToken).ConfigureAwait(false);
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
    private ToolRequest Request(IReadOnlyList<string> arguments, AgeIdentityRef? identity, ReadOnlyMemory<byte> standardInput, string directory)
    {
        Dictionary<string, string> environment = new(StringComparer.Ordinal)
        {
            ["HOME"] = directory
        };
        if (identity is not null)
        {
            environment[AgeKeyFile] = identity.Path;
        }

        return new ToolRequest(_sops, ["--config", "/dev/null", .. arguments], environment, standardInput, directory);
    }

    private async Task<Result<SecretBytes>> RunForOutput(IReadOnlyList<string> arguments, string absoluteFile, AgeIdentityRef identity, CancellationToken cancellationToken)
    {
        var invalid = ProgramInvocation.CheckFile(absoluteFile);
        if (invalid is not null)
        {
            return Result.Invalid<SecretBytes>(invalid, "file", invalid);
        }

        var (run, problem) = await RunSops(arguments, identity, default, cancellationToken).ConfigureAwait(false);

        // The output moves out of the result; the caller now owns and disposes the bytes.
        return run is null ? ProgramInvocation.Error<SecretBytes>(problem!) : Result.Ok(run.StandardOutput);
    }
}
