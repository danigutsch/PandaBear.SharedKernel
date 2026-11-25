namespace SharedKernel.SecretScanning;

/// <summary>What is known about where a scanned text comes from, which steers what <see cref="SecretScanner"/> looks for.</summary>
public sealed record SecretScanContext
{
    /// <summary>Gets a context that knows nothing about the text.</summary>
    public static SecretScanContext None { get; } = new();

    /// <summary>
    /// Gets the name of the file the text comes from, or <see langword="null"/>. A Steam Desktop Authenticator <c>.maFile</c>,
    /// a <c>MEGA-RECOVERYKEY.txt</c>, and file names that say they hold recovery or backup codes raise what is found.
    /// </summary>
    public string? FileName { get; init; }

    /// <summary>Gets the host name of the account the text belongs to, or <see langword="null"/>. It helps recognise recovery codes of known services.</summary>
    public string? Host { get; init; }

    /// <summary>Gets a value indicating whether a labeled user name line should be reported, because the account has none yet.</summary>
    public bool ExpectsUsername { get; init; }

    /// <summary>Gets a value indicating whether the text belongs to a Bitwarden or Vaultwarden account, whose recovery code it may hold.</summary>
    public bool IsBitwardenAccount { get; init; }

    /// <summary>Gets a value indicating whether only the catalog rules run, without the structural detectors for labeled lines, card numbers, and the like.</summary>
    public bool CatalogRulesOnly { get; init; }
}
