using System.Text;

namespace PandaBear.Secrets.Tool;

/// <summary>
/// Checks that a file given as an identity is a readable regular file made of age secret keys, before sops is asked to try it. Without this a missing,
/// empty, garbage, unreadable, or non-age file makes sops fail, and a failure is not a proof that the identity cannot decrypt.
/// </summary>
internal static class IdentityFileCheck
{
    private const long MaxBytes = 1024 * 1024;
    private const string SecretKeyPrefix = "AGE-SECRET-KEY-1";
    private const int SecretKeyLength = 74;
    private const string Bech32Characters = "QPZRY9X8GF2TVDW0S3JN54KHCE6MUA7L";

    /// <summary>Returns <see langword="null"/> when the file is usable, or the reason it is not. The reason never repeats the file's content.</summary>
    public static async Task<string?> Validate(string path, TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (Directory.Exists(path))
        {
            return $"{path}: is a directory, not an identity file";
        }

        if (!File.Exists(path))
        {
            return $"{path}: does not exist";
        }

        var (bytes, problem) = await GuardedFileReader.Read(path, MaxBytes, timeout, cancellationToken).ConfigureAwait(false);
        if (problem is not null)
        {
            return $"{path}: {problem}";
        }

        string text;
        try
        {
            text = new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return $"{path}: is not a text file of age identities";
        }

        var keys = 0;
        foreach (var raw in text.ReplaceLineEndings("\n").Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            if (line.StartsWith("-----BEGIN", StringComparison.Ordinal))
            {
                return $"{path}: is an SSH private key; SSH identities cannot be proven present or absent here, because an SSH key that is still a recipient would decrypt";
            }

            if (line.StartsWith("AGE-PLUGIN-", StringComparison.Ordinal))
            {
                return $"{path}: holds a plugin identity; plugin identities are not supported for proofs";
            }

            if (line.Length != SecretKeyLength || !line.StartsWith(SecretKeyPrefix, StringComparison.Ordinal) || !line[SecretKeyPrefix.Length..].All(static c => Bech32Characters.Contains(c, StringComparison.Ordinal)))
            {
                return $"{path}: is not an age identity file (a line is not an age secret key)";
            }

            keys++;
        }

        return keys == 0 ? $"{path}: contains no age identity" : null;
    }
}
