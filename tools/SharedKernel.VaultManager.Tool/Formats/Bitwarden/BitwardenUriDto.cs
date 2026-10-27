namespace PandaBear.VaultManager.Tool.Formats.Bitwarden;

/// <summary>Bitwarden login URI.</summary>
/// <param name="Match">URI match detection mode, or null for the default.</param>
/// <param name="Uri">The URI text.</param>
internal sealed record BitwardenUriDto(int? Match, string? Uri);
