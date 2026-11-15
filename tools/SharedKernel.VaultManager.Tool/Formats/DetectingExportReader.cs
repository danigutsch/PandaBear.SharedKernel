using PandaBear.Vault.Formats;

namespace PandaBear.VaultManager.Tool.Formats;

/// <summary>
/// Reads every export format the tool supports through <see cref="VaultFileReader"/>, which detects the format from the
/// content.
/// </summary>
internal sealed class DetectingExportReader : IVaultExportReader
{
    /// <summary>Reader shared by every command; it holds no state.</summary>
    public static DetectingExportReader Instance { get; } = new();

    /// <inheritdoc/>
    public VaultReadResult Read(string path, Func<string, string> passwordProvider) => VaultFileReader.Read(path, passwordProvider);
}
