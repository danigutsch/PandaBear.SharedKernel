using System.Security.Cryptography;
using PandaBear.Results;
using PandaBear.Tooling;

namespace PandaBear.Secrets.Tool;

/// <summary>
/// Implements <c>decrypt</c>: decrypts a whole file or one value (<c>file#key.path</c>) with sops and writes the plaintext to
/// standard output. <c>--output</c> writes it to a new file inside the root with mode 0600, through a temporary file and a rename, and needs <c>--force</c>. Nothing else
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

        var destination = outputPath is null ? null : ResolveOutput(root, outputPath);
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

        await WriteNewOwnerOnlyFile(destination!, secret, cancellationToken).ConfigureAwait(false);
        return ExitCodes.Success;
    }

    /// <summary>Resolves <paramref name="output"/> under <paramref name="root"/>: relative, no <c>..</c>, no symbolic link in any segment, an existing directory, and no file there yet.</summary>
    private static string ResolveOutput(string root, string output)
    {
        if (!RepositoryPath.IsRelative(output))
        {
            throw new InvalidOperationException("--output must be a path inside the root: relative, with '/' separators and without empty, '.', or '..' segments.");
        }

        var destination = RepositoryPath.Resolve(root, output);
        var directory = Path.GetDirectoryName(destination);
        if (directory is null || !Directory.Exists(directory))
        {
            throw new InvalidOperationException("The directory of --output does not exist inside the root; decrypt does not create directories.");
        }

        return File.Exists(destination) || Directory.Exists(destination)
            ? throw new InvalidOperationException("The output file already exists; decrypt never overwrites.")
            : destination;
    }

    /// <summary>
    /// Writes the plaintext to a new file next to <paramref name="destination"/> (mode 0600, created exclusively), then renames it into place without
    /// overwriting. A failure at any step deletes the temporary file, so no partial plaintext is left.
    /// </summary>
    private static async Task WriteNewOwnerOnlyFile(string destination, SecretBytes secret, CancellationToken cancellationToken)
    {
        var temporary = Path.Combine(Path.GetDirectoryName(destination)!, $".{RandomNumberGenerator.GetHexString(32)}.tmp");
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

        try
        {
            var stream = new FileStream(temporary, options);
            cancellationToken.ThrowIfCancellationRequested();
            await using (stream.ConfigureAwait(false))
            {
                secret.WriteTo(stream);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporary, destination, overwrite: false);
        }
        catch (IOException) when (File.Exists(destination))
        {
            File.Delete(temporary);
            throw new InvalidOperationException("The output file already exists; decrypt never overwrites.");
        }
        catch
        {
            File.Delete(temporary);
            throw;
        }
    }
}
