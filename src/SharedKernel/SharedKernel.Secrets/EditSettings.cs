namespace PandaBear.Secrets;

/// <summary>What <see cref="SecretsRepository.Edit"/> edits and with what.</summary>
public sealed record EditSettings
{
    /// <summary>Gets the encrypted YAML or JSON file, relative to the root.</summary>
    public required string File { get; init; }

    /// <summary>Gets the absolute path of an identity file that can decrypt the data key.</summary>
    public required string IdentityPath { get; init; }

    /// <summary>Gets the editor command, the text of <c>VISUAL</c> or <c>EDITOR</c> (see <see cref="EditorCommand"/>).</summary>
    public required string? Editor { get; init; }

    /// <summary>
    /// Gets an existing absolute directory on a memory-backed file system (tmpfs or ramfs), such as
    /// <c>$XDG_RUNTIME_DIR</c> or <c>/dev/shm</c>, in which a private directory for the plaintext is created and removed.
    /// </summary>
    public required string TemporaryRoot { get; init; }

    /// <summary>Gets the values of the <see cref="TerminalVariableNames"/> that the host has; any other name is ignored.</summary>
    public IReadOnlyDictionary<string, string> TerminalEnvironment { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// Gets the names of the variables that the editor may receive from the host, so it can work with the terminal or the
    /// display. <c>PATH</c> keeps only its absolute directories.
    /// </summary>
    public static IReadOnlyList<string> TerminalVariableNames { get; } =
        ["TERM", "COLORTERM", "LANG", "LC_ALL", "LC_CTYPE", "LC_MESSAGES", "PATH", "DISPLAY", "WAYLAND_DISPLAY", "XDG_RUNTIME_DIR"];
}
