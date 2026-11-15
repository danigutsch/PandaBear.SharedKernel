namespace PandaBear.Vault.Pipeline;

/// <summary>Password prompts of a <see cref="VaultPipeline"/>. Passwords are never read from arguments or the environment.</summary>
/// <param name="ReadPassword">Prompts for an existing password, such as the password of an input file, and returns it.</param>
/// <param name="ReadNewPassword">Prompts for a new password with confirmation, such as the password of an output file, and returns it.</param>
public sealed record VaultPasswordPrompts(Func<string, string> ReadPassword, Func<string, string> ReadNewPassword);
