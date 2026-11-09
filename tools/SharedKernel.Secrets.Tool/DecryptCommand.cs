using PandaBear.Results;

namespace PandaBear.Secrets.Tool;

/// <summary>
/// Implements <c>decrypt</c>: decrypts a whole file or one value (<c>file#key.path</c>) with sops and writes the plaintext to
/// standard output. <c>--output</c> writes it to a new file with mode 0600 instead, and needs <c>--force</c>. Nothing else
/// goes to standard output, and the plaintext is never escaped: it is the data.
/// </summary>
internal static class DecryptCommand
{
    private const string IdentityVariable = "SOPS_AGE_KEY_FILE";

    /// <summary>Runs the command and returns its exit code.</summary>
    /// <exception cref="InvalidOperationException">The input is invalid, <c>--output</c> lacks <c>--force</c>, the output exists, a tool is missing, or sops failed.</exception>
    public static async Task<int> Run(
        SecretsEnvironment environment,
        string root,
        string target,
        string? identityPath,
        string? outputPath,
        bool force,
        CancellationToken cancellationToken)
    {
        if (outputPath is not null && !force)
        {
            throw new InvalidOperationException("--output writes decrypted plaintext to a file and needs --force. Without --output the plaintext goes to standard output.");
        }

        var identityText = identityPath ?? environment.GetEnvironmentVariable(IdentityVariable);
        if (string.IsNullOrEmpty(identityText))
        {
            throw new InvalidOperationException($"No identity file: pass --identity <path> or set {IdentityVariable} to the path of an identity file.");
        }

        var identity = AgeIdentityRef.Create(Path.GetFullPath(identityText, environment.WorkingDirectory));
        if (identity.IsFailure)
        {
            throw new InvalidOperationException(identity.ErrorDetails?.Detail ?? "The identity file path is invalid.");
        }

        var sops = ToolLocator.Sops(environment);
        Result<SecretBytes> plaintext;
        if (target.Contains('#', StringComparison.Ordinal))
        {
            var reference = SecretRef.Create(target, "target");
            plaintext = reference.IsFailure
                ? throw new InvalidOperationException(reference.ErrorDetails?.Detail ?? "The secret reference is invalid.")
                : await sops.DecryptValue(root, reference.Value, identity.Value, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            var path = EncryptedFilePath.Create(target, "target");
            var resolved = path.IsFailure ? throw new InvalidOperationException(path.ErrorDetails?.Detail ?? "The file path is invalid.") : path.Value.ResolveUnder(root);
            plaintext = resolved.IsFailure
                ? throw new InvalidOperationException(resolved.ErrorDetails?.Detail ?? "The file path cannot be resolved under the root.")
                : await sops.Decrypt(resolved.Value, identity.Value, cancellationToken).ConfigureAwait(false);
        }

        if (plaintext.IsFailure)
        {
            throw new InvalidOperationException(plaintext.ErrorDetails?.Detail ?? "sops failed.");
        }

        using var secret = plaintext.Value;
        if (outputPath is null)
        {
            var standardOutput = environment.OpenStandardOutput();
            await using (standardOutput.ConfigureAwait(false))
            {
                secret.WriteTo(standardOutput);
                await standardOutput.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            return ExitCodes.Success;
        }

        await WriteNewOwnerOnlyFile(Path.GetFullPath(outputPath, environment.WorkingDirectory), secret).ConfigureAwait(false);
        return ExitCodes.Success;
    }

    private static async Task WriteNewOwnerOnlyFile(string path, SecretBytes secret)
    {
        FileStreamOptions options = new()
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.Write,
            Share = FileShare.None
        };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        FileStream stream;
        try
        {
            stream = new FileStream(path, options);
        }
        catch (IOException) when (File.Exists(path))
        {
            throw new InvalidOperationException("The output file already exists; decrypt never overwrites.");
        }

        await using (stream.ConfigureAwait(false))
        {
            secret.WriteTo(stream);
        }
    }
}
