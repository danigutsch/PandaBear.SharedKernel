namespace PandaBear.VaultManager.Tool.Brief;

/// <summary>An input file a brief was made from.</summary>
/// <param name="File">File name of the input.</param>
/// <param name="Sha256">SHA-256 of the file's bytes, in lowercase hexadecimal.</param>
internal sealed record BriefInput(string File, string Sha256);
