using PandaBear.SecretScanning;
using PandaBear.Vault.Formats;
using PandaBear.Vault.Model;

namespace PandaBear.Vault.Secrets;

/// <summary>
/// Converts private keys with <see cref="SshKeyConverter"/> into Bitwarden SSH key data, and refuses a key that does not fit
/// Bitwarden's field budget, before and after conversion.
/// </summary>
internal static class BitwardenSshKeys
{
    private const string BudgetProblem = "key exceeds the Bitwarden field budget";

    /// <summary>Inspects a key like <see cref="SshKeyConverter.Inspect"/>; a convertible key that does not fit cannot be converted.</summary>
    /// <param name="pem">Private key text.</param>
    /// <returns>The inspection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="pem"/> is <see langword="null"/>.</exception>
    public static SshKeyInspection Inspect(string pem)
    {
        SshKeyInspection inspection = SshKeyConverter.Inspect(pem);
        return inspection.CanConvert && !BitwardenLimits.Fits(pem, BitwardenLimits.ValueEncryptedLength)
            ? inspection with
            {
                CanConvert = false,
                Problem = BudgetProblem,
            }
            : inspection;
    }

    /// <summary>Converts a key into SSH key data, or says why it was not converted.</summary>
    /// <param name="pem">Private key text.</param>
    /// <param name="suppliedPublicKey">Matching public key in authorized_keys form, used for Ed25519 PKCS#8 keys.</param>
    /// <returns>The key data, or the problem that prevented conversion.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="pem"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The inspected key can no longer be parsed as a PEM block.</exception>
    public static BitwardenSshKey Convert(string pem, string? suppliedPublicKey = null)
    {
        // SshKeyConverter.Convert inspects the key itself; only a key that does not fit needs the inspection here.
        if (!BitwardenLimits.Fits(pem, BitwardenLimits.ValueEncryptedLength))
        {
            return new BitwardenSshKey(null, Inspect(pem).Problem);
        }

        SshKeyConversion conversion = SshKeyConverter.Convert(pem, suppliedPublicKey);
        if (conversion.Key is not { } pair)
        {
            return new BitwardenSshKey(null, conversion.Problem);
        }

        return BitwardenLimits.Fits(pair.PrivateKey, BitwardenLimits.ValueEncryptedLength)
            ? new BitwardenSshKey(new SshKeyData { PrivateKey = pair.PrivateKey, PublicKey = pair.PublicKey, KeyFingerprint = pair.Fingerprint }, null)
            : new BitwardenSshKey(null, BudgetProblem);
    }
}
