using System.Text;
using PandaBear.Results;

namespace PandaBear.Secrets;

/// <summary>
/// <see cref="ISopsTool"/> over the <c>sops</c> program, started from an absolute path without a shell. Plaintext goes in on
/// standard input and out on standard output; arguments carry only public recipients, paths, and key paths; the only
/// environment variable is <c>SOPS_AGE_KEY_FILE</c>, set to the identity file's path. Reading metadata does not start a process.
/// </summary>
public sealed class ProcessSopsTool : ISopsTool
{
    private const int MaxFileBytes = 16 * 1024 * 1024;
    private const string AgeKeyFile = "SOPS_AGE_KEY_FILE";

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

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="recipients"/> is <see langword="null"/>.</exception>
    public async Task<Result<string>> Encrypt(SopsFormat format, AgeRecipientSet recipients, ReadOnlyMemory<byte> plaintext, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recipients);
        var type = FormatName(format);
        string[] arguments = ["encrypt", "--age", string.Join(',', recipients.Recipients.Select(static item => item.Value)), "--input-type", type, "--output-type", type, "/dev/stdin"];
        var (run, problem) = await ToolInvocation.Run(_runner, "sops", new ToolRequest(_sops, arguments, standardInput: plaintext), cancellationToken).ConfigureAwait(false);
        if (run is null)
        {
            return ToolInvocation.Error<string>(problem!);
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
    public async Task<Result> Rotate(string absoluteFile, AgeIdentityRef identity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(absoluteFile);
        ArgumentNullException.ThrowIfNull(identity);
        var invalid = ToolInvocation.CheckFile(absoluteFile);
        if (invalid is not null)
        {
            return Result.Invalid(invalid, "file", invalid);
        }

        var (run, problem) = await ToolInvocation.Run(_runner, "sops", Request(["rotate", "-i", absoluteFile], identity), cancellationToken).ConfigureAwait(false);
        if (run is null)
        {
            return Result.Error(problem!);
        }

        run.Dispose();
        return Result.Ok();
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public async Task<Result<RecipientVerification>> UpdateRecipients(string absoluteFile, SopsCreationRule rule, AgeIdentityRef identity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(absoluteFile);
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(identity);
        var before = await VerifyRecipients(absoluteFile, rule, cancellationToken).ConfigureAwait(false);
        if (before.IsFailure || before.Value.IsExact)
        {
            return before;
        }

        if (before.Value.OtherKeyTypes.Count > 0)
        {
            return ToolInvocation.Error<RecipientVerification>("The file has non-age key holders; sops rotate cannot remove them, so the recipients are not changed.");
        }

        List<string> arguments = ["rotate", "-i"];
        if (before.Value.Missing.Count > 0)
        {
            arguments.AddRange(["--add-age", string.Join(',', before.Value.Missing.Select(static item => item.Value))]);
        }

        if (before.Value.Unexpected.Count > 0)
        {
            arguments.AddRange(["--rm-age", string.Join(',', before.Value.Unexpected.Select(static item => item.Value))]);
        }

        arguments.Add(absoluteFile);
        var (run, problem) = await ToolInvocation.Run(_runner, "sops", Request(arguments, identity), cancellationToken).ConfigureAwait(false);
        if (run is null)
        {
            return ToolInvocation.Error<RecipientVerification>(problem!);
        }

        run.Dispose();
        var after = await VerifyRecipients(absoluteFile, rule, cancellationToken).ConfigureAwait(false);
        return after.IsSuccess && after.Value.IsExact
            ? before
            : ToolInvocation.Error<RecipientVerification>("sops finished, but the file's recipients are still not the rule's recipient set.");
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="absoluteFile"/> is <see langword="null"/>.</exception>
    public async Task<Result<SopsFileRecipients>> ListRecipients(string absoluteFile, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(absoluteFile);
        var invalid = ToolInvocation.CheckFile(absoluteFile);
        if (invalid is not null)
        {
            return Result.Invalid<SopsFileRecipients>(invalid, "file", invalid);
        }

        var format = Path.GetExtension(absoluteFile) switch
        {
            ".yaml" or ".yml" => SopsFormat.Yaml,
            ".json" => SopsFormat.Json,
            _ => (SopsFormat?)null,
        };
        if (format is null)
        {
            return ToolInvocation.Error<SopsFileRecipients>("Only YAML and JSON SOPS files have readable recipients.");
        }

        string text;
        try
        {
            text = await ReadBounded(absoluteFile, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return Result.NotFound<SopsFileRecipients>("The file does not exist.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return ToolInvocation.Error<SopsFileRecipients>("The file cannot be read.");
        }

        return text.Length == 0 ? ToolInvocation.Error<SopsFileRecipients>("The file is empty or larger than 16 MiB.") : SopsMetadataReader.Read(text, format.Value);
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public async Task<Result<RecipientVerification>> VerifyRecipients(string absoluteFile, SopsCreationRule rule, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(rule);
        var actual = await ListRecipients(absoluteFile, cancellationToken).ConfigureAwait(false);
        return actual.IsFailure ? ToolInvocation.Error<RecipientVerification>(actual.ErrorDetails?.Detail ?? "The recipients cannot be read.") : Result.Ok(RecipientVerification.Compare(actual.Value, rule));
    }

    private static string FormatName(SopsFormat format) => format == SopsFormat.Json ? "json" : "yaml";

    private ToolRequest Request(IReadOnlyList<string> arguments, AgeIdentityRef identity) =>
        new(_sops, arguments, new Dictionary<string, string> { [AgeKeyFile] = identity.Path });

    private async Task<Result<SecretBytes>> RunForOutput(IReadOnlyList<string> arguments, string absoluteFile, AgeIdentityRef identity, CancellationToken cancellationToken)
    {
        var invalid = ToolInvocation.CheckFile(absoluteFile);
        if (invalid is not null)
        {
            return Result.Invalid<SecretBytes>(invalid, "file", invalid);
        }

        var (run, problem) = await ToolInvocation.Run(_runner, "sops", Request(arguments, identity), cancellationToken).ConfigureAwait(false);

        // The output moves out of the result; the caller now owns and disposes the bytes.
        return run is null ? ToolInvocation.Error<SecretBytes>(problem!) : Result.Ok(run.StandardOutput);
    }

    private static async Task<string> ReadBounded(string path, CancellationToken cancellationToken)
    {
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
        await using (stream.ConfigureAwait(false))
        {
            var buffer = new byte[MaxFileBytes + 1];
            var length = 0;
            int read;
            while (length < buffer.Length && (read = await stream.ReadAsync(buffer.AsMemory(length), cancellationToken).ConfigureAwait(false)) > 0)
            {
                length += read;
            }

            return length > MaxFileBytes ? string.Empty : Encoding.UTF8.GetString(buffer.AsSpan(0, length));
        }
    }
}
