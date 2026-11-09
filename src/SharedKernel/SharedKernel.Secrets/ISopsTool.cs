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

    /// <summary>Rotates the file's data key in place (<c>sops rotate -i</c>). The recipients stay.</summary>
    /// <param name="absoluteFile">The encrypted file, rewritten in place.</param>
    /// <param name="identity">The identity file that can decrypt it.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    Task<Result> Rotate(string absoluteFile, AgeIdentityRef identity, CancellationToken cancellationToken);

    /// <summary>
    /// Makes the file's recipients equal the rule's recipient set, in place: it rotates the data key while adding the missing
    /// and removing the unexpected recipients (<c>sops rotate -i --add-age --rm-age</c>), then reads the file again and
    /// fails unless the set is exact. A file with non-age key holders is refused. Returns the comparison made before the change.
    /// </summary>
    /// <param name="absoluteFile">The encrypted file, rewritten in place.</param>
    /// <param name="rule">The rule whose recipient set the file must end up with.</param>
    /// <param name="identity">The identity file that can decrypt it.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    Task<Result<RecipientVerification>> UpdateRecipients(string absoluteFile, SopsCreationRule rule, AgeIdentityRef identity, CancellationToken cancellationToken);

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
