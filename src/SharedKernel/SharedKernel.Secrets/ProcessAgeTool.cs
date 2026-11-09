using System.Text;
using PandaBear.Results;

namespace PandaBear.Secrets;

/// <summary>
/// <see cref="IAgeTool"/> over the <c>age</c> and <c>age-keygen</c> programs, started from absolute paths without a shell. Recipients and
/// identity file paths are arguments; plaintext and ciphertext are standard input and output; the child gets no environment variables.
/// </summary>
public sealed class ProcessAgeTool : IAgeTool
{
    private const string PublicKeyPrefix = "Public key: ";

    private readonly IToolRunner _runner;
    private readonly string _age;
    private readonly string _keygen;

    /// <summary>Creates the adapter.</summary>
    /// <param name="runner">Runs the programs.</param>
    /// <param name="agePath">The absolute path of <c>age</c>.</param>
    /// <param name="ageKeygenPath">The absolute path of <c>age-keygen</c>.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A path is not absolute.</exception>
    public ProcessAgeTool(IToolRunner runner, string agePath, string ageKeygenPath)
    {
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(agePath);
        ArgumentNullException.ThrowIfNull(ageKeygenPath);
        if (!Path.IsPathFullyQualified(agePath) || !Path.IsPathFullyQualified(ageKeygenPath))
        {
            throw new ArgumentException("The tool paths must be absolute.");
        }

        _runner = runner;
        _age = agePath;
        _keygen = ageKeygenPath;
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="destination"/> is <see langword="null"/>.</exception>
    public async Task<Result<AgeRecipient>> GenerateIdentity(AgeIdentityRef destination, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (File.Exists(destination.Path) || Directory.Exists(destination.Path))
        {
            return Result.Conflict<AgeRecipient>("The identity file already exists; it is never overwritten.");
        }

        var directory = Path.GetDirectoryName(destination.Path);
        if (directory is null || !Directory.Exists(directory))
        {
            return ToolInvocation.Error<AgeRecipient>("The directory of the identity file does not exist.");
        }

        var (run, problem) = await ToolInvocation.Run(_runner, "age-keygen", new ToolRequest(_keygen, ["-o", destination.Path]), cancellationToken).ConfigureAwait(false);
        if (run is null)
        {
            return ToolInvocation.Error<AgeRecipient>(problem!);
        }

        using (run)
        {
            var recipient = FindPublicKey(run.StandardError) ?? FindPublicKey(Encoding.UTF8.GetString(run.StandardOutput.AsSpan()));
            if (recipient is null)
            {
                return ToolInvocation.Error<AgeRecipient>("age-keygen did not print a public key.");
            }

            var parsed = AgeRecipient.Create(recipient);
            if (parsed.IsFailure)
            {
                return ToolInvocation.Error<AgeRecipient>("age-keygen printed a public key that is not a valid recipient.");
            }

            if (!OperatingSystem.IsWindows() && File.Exists(destination.Path) && (File.GetUnixFileMode(destination.Path) & (UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute)) != 0)
            {
                return ToolInvocation.Error<AgeRecipient>("The identity file was created readable by others; restrict it to the owner.");
            }

            return parsed;
        }
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="recipients"/> is <see langword="null"/>.</exception>
    public async Task<Result<string>> Encrypt(AgeRecipientSet recipients, ReadOnlyMemory<byte> plaintext, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recipients);
        List<string> arguments = ["--encrypt", "--armor"];
        foreach (var recipient in recipients.Recipients)
        {
            arguments.Add("-r");
            arguments.Add(recipient.Value);
        }

        var (run, problem) = await ToolInvocation.Run(_runner, "age", new ToolRequest(_age, arguments, standardInput: plaintext), cancellationToken).ConfigureAwait(false);
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
    /// <exception cref="ArgumentNullException"><paramref name="identity"/> is <see langword="null"/>.</exception>
    public async Task<Result<SecretBytes>> Decrypt(AgeIdentityRef identity, ReadOnlyMemory<byte> ciphertext, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identity);
        var (run, problem) = await ToolInvocation.Run(_runner, "age", new ToolRequest(_age, ["--decrypt", "-i", identity.Path], standardInput: ciphertext), cancellationToken).ConfigureAwait(false);
        // The output moves out of the result; the caller now owns and disposes the bytes.
        return run is null ? ToolInvocation.Error<SecretBytes>(problem!) : Result.Ok(run.StandardOutput);
    }

    private static string? FindPublicKey(string text)
    {
        foreach (var line in text.Split('\n', StringSplitOptions.TrimEntries))
        {
            if (line.StartsWith(PublicKeyPrefix, StringComparison.Ordinal))
            {
                return line[PublicKeyPrefix.Length..].Trim();
            }
        }

        return null;
    }
}
