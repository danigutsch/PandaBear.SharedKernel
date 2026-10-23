namespace PandaBear.VaultManager.Tool.Secrets;

/// <summary>How certain a finding is, ordered from weakest to strongest.</summary>
internal enum Confidence
{
    /// <summary>Entropy alone.</summary>
    Suggestion = 0,

    /// <summary>Entropy plus a keyword.</summary>
    Low = 1,

    /// <summary>Prefix only, or structure plus context.</summary>
    Medium = 2,

    /// <summary>Prefix plus checksum, or a strong validator such as Luhn, IBAN, BIP39, otpauth, or PEM structure.</summary>
    High = 3,
}
