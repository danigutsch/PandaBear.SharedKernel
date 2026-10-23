namespace PandaBear.VaultManager.Tool.Pipeline;

/// <summary>Where and how the merged vault is written.</summary>
/// <param name="Path">Output file. It must not be one of the input files.</param>
/// <param name="Plaintext">
/// Whether to write unencrypted Bitwarden JSON instead of a password-protected export. Set it only on an explicit
/// request.
/// </param>
/// <param name="Force">Whether an existing file may be replaced.</param>
internal sealed record PipelineOutput(string Path, bool Plaintext = false, bool Force = false);
