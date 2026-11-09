using PandaBear.Results;

namespace PandaBear.Secrets;

/// <summary>
/// The <c>sops</c> operations that work without an editor. File paths are absolute (resolve a repository-relative
/// <see cref="EncryptedFilePath"/> with <see cref="EncryptedFilePath.ResolveUnder"/>). The identity is passed as the path
/// in <c>SOPS_AGE_KEY_FILE</c>, the only environment variable the child gets.
/// </summary>
public interface ISopsTool
{
    /// <summary>Encrypts <paramref name="plaintext"/> (a document in <paramref name="format"/>) to exactly <paramref name="recipients"/> and returns the encrypted document.</summary>
    /// <param name="format">The document format.</param>
    /// <param name="recipients">The recipients, passed as <c>--age</c>.</param>
    /// <param name="plaintext">The document, written to the tool's standard input.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    Task<Result<string>> Encrypt(SopsFormat format, AgeRecipientSet recipients, ReadOnlyMemory<byte> plaintext, CancellationToken cancellationToken);

    /// <summary>Decrypts a whole file to its standard output. The caller disposes the result.</summary>
    /// <param name="absoluteFile">The encrypted file.</param>
    /// <param name="identity">The identity file.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    Task<Result<SecretBytes>> Decrypt(string absoluteFile, AgeIdentityRef identity, CancellationToken cancellationToken);

    /// <summary>Decrypts the single value that <paramref name="reference"/> names, under <paramref name="root"/>. The caller disposes the result.</summary>
    /// <param name="root">The absolute root directory of the reference's file.</param>
    /// <param name="reference">The value to extract.</param>
    /// <param name="identity">The identity file.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    Task<Result<SecretBytes>> DecryptValue(string root, SecretRef reference, AgeIdentityRef identity, CancellationToken cancellationToken);

    /// <summary>
    /// Tries to decrypt the file with the identity and discards the plaintext unread. Only a failure that sops attributes to the identity not matching
    /// any recipient is <see cref="SopsTrialOutcome.NoMatchingIdentity"/>; every other failure is <see cref="SopsTrialOutcome.Failed"/>.
    /// </summary>
    /// <param name="absoluteFile">The encrypted file.</param>
    /// <param name="identity">The identity file.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    Task<SopsTrial> TrialDecrypt(string absoluteFile, AgeIdentityRef identity, CancellationToken cancellationToken);

    /// <summary>Reads the key holders from the file's metadata without decrypting it.</summary>
    /// <param name="absoluteFile">The encrypted YAML or JSON file.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    Task<Result<SopsFileRecipients>> ListRecipients(string absoluteFile, CancellationToken cancellationToken);

    /// <summary>Checks that the file's key holders equal the rule's recipient set exactly, from its metadata.</summary>
    /// <param name="absoluteFile">The encrypted YAML or JSON file.</param>
    /// <param name="rule">The rule that applies to it.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    Task<Result<RecipientVerification>> VerifyRecipients(string absoluteFile, SopsCreationRule rule, CancellationToken cancellationToken);
}
