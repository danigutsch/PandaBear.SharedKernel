using System.Security.Cryptography;
using System.Text;
using PandaBear.Results;

namespace PandaBear.Secrets;

/// <summary>
/// Encrypts a new file: finds the creation rule for a new file, previews, or with <c>--force</c> reads the plaintext
/// from standard input, encrypts it with sops to exactly the rule's recipients, and creates the file. It never overwrites.
/// </summary>
internal static class EncryptOperation
{
    /// <summary>Runs the operation and returns its exit code.</summary>
    /// <exception cref="InvalidOperationException">The input is invalid, no rule matches, the file exists, a tool is missing or failed, or the result is not exact.</exception>
    public static async Task<int> Run(string root, SecretsPrograms programs, EncryptSettings settings, TextWriter output, CancellationToken cancellationToken)
    {
        var rules = await SecretsConfigurationFile.Read(root, cancellationToken).ConfigureAwait(false);
        var path = EncryptedFilePath.Create(settings.File);
        if (path.IsFailure)
        {
            throw new InvalidOperationException(path.ErrorDetails?.Detail ?? "The file path is invalid.");
        }

        var rule = rules.FirstMatch(path.Value) ?? throw new InvalidOperationException("No creation rule matches this file.");
        var format = path.Value.Format ?? throw new InvalidOperationException("Only YAML and JSON files can be encrypted.");
        var target = path.Value.ResolveUnder(root);
        if (target.IsFailure)
        {
            throw new InvalidOperationException(target.ErrorDetails?.Detail ?? "The file path cannot be resolved under the root.");
        }

        if (File.Exists(target.Value) || Directory.Exists(target.Value))
        {
            throw new InvalidOperationException("The file already exists; encrypt never overwrites. Use sops to edit or rotate it.");
        }

        var sops = programs.Sops();
        if (!settings.Force)
        {
            await EscapedOutput.Line(output, $"Preview: would encrypt standard input to {path.Value} for {rule.Recipients.Count} recipients (rule {rule.PathRegex}).", cancellationToken).ConfigureAwait(false);
            await EscapedOutput.Line(output, "Standard input was not read and nothing was written. Run again with --force to encrypt.", cancellationToken).ConfigureAwait(false);
            return SecretsExitCodes.Success;
        }

        var plaintext = await PlaintextInput.Read(settings.OpenPlaintext(), cancellationToken).ConfigureAwait(false);
        Result<string> encrypted;
        try
        {
            encrypted = await sops.Encrypt(format, rule.Recipients, plaintext, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }

        if (encrypted.IsFailure)
        {
            throw new InvalidOperationException(encrypted.ErrorDetails?.Detail ?? "sops failed.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(target.Value)!);
        var created = new FileStream(target.Value, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, useAsync: true);
        await using (created.ConfigureAwait(false))
        {
            await created.WriteAsync(Encoding.UTF8.GetBytes(encrypted.Value), cancellationToken).ConfigureAwait(false);
        }

        var check = await SopsFileReader.ReadRecipients(target.Value, cancellationToken).ConfigureAwait(false);
        if (check.IsFailure || !RecipientVerification.Compare(check.Value, rule).IsExact)
        {
            File.Delete(target.Value);
            throw new InvalidOperationException("The encrypted file does not carry exactly the rule's recipients; it was removed.");
        }

        await EscapedOutput.Line(output, $"Encrypted {path.Value} to {rule.Recipients.Count} recipients.", cancellationToken).ConfigureAwait(false);
        return SecretsExitCodes.Success;
    }
}
