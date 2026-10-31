using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace PandaBear.VaultManager.Tool.Secrets;

/// <summary>Classifies multilingual notes labels after lowercasing and removing <c>_ - .</c> and spaces.</summary>
internal static class KeyClassifier
{
    // Checked in order: more specific classes first, so "otp secret" is TOTP and "recovery phrase" is a seed.
    private static readonly (KeyClass Class, string[] Keys)[] Classes =
    [
        (KeyClass.Totp, ["totp", "2fa", "otpsecret", "otp", "totpsecret", "2fasecret", "authenticatorkey", "mfasecret"]),
        (KeyClass.Seed, ["seed", "seedphrase", "mnemonic", "recoveryphrase", "seedwords"]),
        (KeyClass.Recovery, ["recovery", "recoverycode", "recoverycodes", "recoverykey", "backupcode", "backupcodes", "wiederherstellungscode", "wiederherstellungscodes", "scratchcodes"]),
        (KeyClass.Pin, ["pin", "pincode", "geheimzahl", "passcode"]),
        (KeyClass.Password, ["password", "passwort", "kennwort", "pass", "pwd", "pw", "passphrase", "motdepasse", "contraseña", "senha", "wachtwoord"]),
        (KeyClass.Token, ["token", "apikey", "secret", "clientsecret", "accesskey", "apitoken", "accesstoken", "secretkey", "authtoken"]),
        (KeyClass.Username, ["user", "username", "login", "benutzername", "email", "benutzer", "userid"]),
        (KeyClass.Question, ["q", "question", "securityquestion", "frage", "sicherheitsfrage"]),
        (KeyClass.Answer, ["a", "answer", "antwort", "securityanswer"]),
        (KeyClass.Iban, ["iban"]),
        (KeyClass.Bic, ["bic", "swift", "swiftcode", "biccode"]),
    ];

    /// <summary>Normalizes a label: lowercase, without <c>_ - .</c> and spaces.</summary>
    /// <param name="key">Label.</param>
    /// <returns>Normalized label.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is <see langword="null"/>.</exception>
    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "Labels are matched against lowercase multilingual keywords.")]
    public static string Normalize(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        var builder = new StringBuilder(key.Length);
        foreach (char character in key.ToLowerInvariant())
        {
            if (character is not ('_' or '-' or '.' or ' ' or '\t'))
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }

    /// <summary>Classifies a label. Long keywords also match as a suffix, such as <c>wifi password</c>.</summary>
    /// <param name="key">Label as written.</param>
    /// <returns>The class.</returns>
    public static KeyClass Classify(string key)
    {
        string normalized = Normalize(key);
        foreach (var (keyClass, keys) in Classes)
        {
            foreach (string candidate in keys)
            {
                if (normalized == candidate || (candidate.Length >= 5 && normalized.EndsWith(candidate, StringComparison.Ordinal)))
                {
                    return keyClass;
                }
            }
        }

        return KeyClass.None;
    }
}
