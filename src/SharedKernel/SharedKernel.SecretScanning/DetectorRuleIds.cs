using System.Collections.Frozen;

namespace PandaBear.SecretScanning;

/// <summary>Rule identifiers of the findings that the structural detectors report, as opposed to catalog rules.</summary>
internal static class DetectorRuleIds
{
    /// <summary>A MEGA recovery key file.</summary>
    public const string MegaRecoveryKey = "mega-recovery-key";

    /// <summary>A line whose label pattern timed out.</summary>
    public const string NotesLine = "notes-line";

    /// <summary>A security question and its answer.</summary>
    public const string SecurityQuestion = "security-question";

    /// <summary>A labeled BIP39 passphrase line.</summary>
    public const string NotesPassphrase = "notes-passphrase";

    /// <summary>A labeled password line.</summary>
    public const string NotesPassword = "notes-password";

    /// <summary>A labeled PIN line.</summary>
    public const string NotesPin = "notes-pin";

    /// <summary>A labeled user name line.</summary>
    public const string NotesUsername = "notes-username";

    /// <summary>A labeled token line.</summary>
    public const string NotesToken = "notes-token";

    /// <summary>A Bitwarden account recovery code.</summary>
    public const string BitwardenRecoveryCode = "bitwarden-recovery-code";

    /// <summary>A labeled recovery code line.</summary>
    public const string NotesRecoveryCode = "notes-recovery-code";

    /// <summary>A labeled seed phrase line.</summary>
    public const string NotesSeed = "notes-seed";

    /// <summary>A labeled TOTP secret line.</summary>
    public const string NotesTotp = "notes-totp";

    /// <summary>A labeled BIC line.</summary>
    public const string Bic = "bic";

    /// <summary>A card number that passes the Luhn check.</summary>
    public const string CardNumber = "card-number";

    /// <summary>An IBAN with a valid check digit.</summary>
    public const string Iban = "iban";

    /// <summary>A BIP39 seed phrase.</summary>
    public const string Bip39Seed = "bip39-seed";

    /// <summary>An Electrum seed phrase.</summary>
    public const string ElectrumSeed = "electrum-seed";

    /// <summary>A long high-entropy string.</summary>
    public const string GenericHighEntropy = "generic-high-entropy";

    /// <summary>A Steam Desktop Authenticator file whose patterns timed out.</summary>
    public const string SteamMaFile = "steam-mafile";

    /// <summary>A Steam Guard revocation code in a Steam Desktop Authenticator file.</summary>
    public const string SteamMaFileRevocationCode = "steam-mafile-revocation-code";

    /// <summary>A Steam Guard shared secret in a Steam Desktop Authenticator file.</summary>
    public const string SteamMaFileSharedSecret = SecretScanner.MaFileSharedSecretRule;

    /// <summary>A block of recovery codes.</summary>
    public const string RecoveryCodeBlock = RecoveryCodeDetector.RuleId;

    /// <summary>Every detector rule identifier.</summary>
    public static FrozenSet<string> All { get; } = new[]
    {
        MegaRecoveryKey,
        NotesLine,
        SecurityQuestion,
        NotesPassphrase,
        NotesPassword,
        NotesPin,
        NotesUsername,
        NotesToken,
        BitwardenRecoveryCode,
        NotesRecoveryCode,
        NotesSeed,
        NotesTotp,
        Bic,
        CardNumber,
        Iban,
        Bip39Seed,
        ElectrumSeed,
        GenericHighEntropy,
        SteamMaFile,
        SteamMaFileRevocationCode,
        SteamMaFileSharedSecret,
        RecoveryCodeBlock,
    }.ToFrozenSet(StringComparer.Ordinal);
}
