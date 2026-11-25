using SharedKernel.Results;

namespace SharedKernel.Secrets;

/// <summary>The <c>age</c> and <c>age-keygen</c> operations. Plaintext and ciphertext travel on standard input and output only.</summary>
public interface IAgeTool
{
    /// <summary>
    /// Runs <c>age-keygen</c>, which writes a new identity file with mode 0600 at <paramref name="destination"/>. The file
    /// must not exist and is never overwritten. Only the public recipient is returned.
    /// </summary>
    /// <param name="destination">The identity file to create.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    Task<Result<AgeRecipient>> GenerateIdentity(AgeIdentityRef destination, CancellationToken cancellationToken);

    /// <summary>Encrypts <paramref name="plaintext"/> to <paramref name="recipients"/> and returns the ASCII-armored ciphertext.</summary>
    /// <param name="recipients">The recipients that can decrypt.</param>
    /// <param name="plaintext">The bytes to encrypt, written to the tool's standard input.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    Task<Result<string>> Encrypt(AgeRecipientSet recipients, ReadOnlyMemory<byte> plaintext, CancellationToken cancellationToken);

    /// <summary>Decrypts armored or binary <paramref name="ciphertext"/> with the identity file. The caller disposes the result.</summary>
    /// <param name="identity">The identity file.</param>
    /// <param name="ciphertext">The bytes to decrypt, written to the tool's standard input.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    Task<Result<SecretBytes>> Decrypt(AgeIdentityRef identity, ReadOnlyMemory<byte> ciphertext, CancellationToken cancellationToken);
}
