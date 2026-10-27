namespace PandaBear.VaultManager.Tool.Formats.Bitwarden;

/// <summary>Bitwarden secure note object; type 0 is the only defined note type.</summary>
internal sealed record BitwardenSecureNoteDto(int Type);
