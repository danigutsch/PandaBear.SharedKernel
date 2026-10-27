namespace PandaBear.VaultManager.Tool.Formats.Bitwarden;

/// <summary>Bitwarden secure note object; type 0 is the only defined note type.</summary>
/// <param name="Type">Note type; 0 is the only defined value.</param>
internal sealed record BitwardenSecureNoteDto(int Type);
