using PandaBear.VaultManager.Tool.Merge;

namespace PandaBear.VaultManager.Tool.Cli;

/// <summary>Command-line values of a merge.</summary>
/// <param name="Inputs">Vault export files, in order.</param>
/// <param name="Output">File to write the merged vault to.</param>
/// <param name="Decisions">Decisions file to read and to update with pending entries, or <see langword="null"/>.</param>
/// <param name="Policy">Conflict policy.</param>
/// <param name="Plaintext">Whether to write unencrypted JSON instead of a password-protected export.</param>
/// <param name="Report">Markdown report file, or <see langword="null"/>.</param>
/// <param name="Force">Whether an existing output may be replaced.</param>
internal sealed record MergeArguments(
    IReadOnlyList<string> Inputs,
    string Output,
    string? Decisions,
    MergePolicy Policy,
    bool Plaintext,
    string? Report,
    bool Force);
