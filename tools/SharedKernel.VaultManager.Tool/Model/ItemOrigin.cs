namespace PandaBear.VaultManager.Tool.Model;

/// <summary>Where a vault item was read from.</summary>
/// <param name="Provider">Source vault provider, for example <c>keeper</c> or <c>bitwarden</c>.</param>
/// <param name="SourceFile">File name the item was read from.</param>
/// <param name="OriginalId">Identifier in the source export, when it has one.</param>
/// <param name="Modified">Last modification time reported by the source, when it reports one.</param>
internal sealed record ItemOrigin(string Provider, string SourceFile, string? OriginalId, DateTimeOffset? Modified);
