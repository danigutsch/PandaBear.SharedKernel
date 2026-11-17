namespace PandaBear.Secrets;

/// <summary>Where and with what <see cref="ISopsTool.Edit"/> lets a person edit the decrypted document.</summary>
public sealed record SopsEditSession
{
    /// <summary>Gets the editor that sops starts.</summary>
    public required EditorCommand Editor { get; init; }

    /// <summary>
    /// Gets an existing absolute directory that only the caller can read, on a memory-backed file system. The decrypted
    /// document and every file the editor writes next to it, such as a swap file, stay inside it; it is the child's
    /// <c>HOME</c> and <c>TMPDIR</c>.
    /// </summary>
    public required string PrivateDirectory { get; init; }

    /// <summary>
    /// Gets the variables the editor needs to work with the terminal, such as <c>TERM</c> and <c>LANG</c>. They cannot
    /// replace <c>HOME</c>, <c>TMPDIR</c>, <c>EDITOR</c>, or the identity.
    /// </summary>
    public IReadOnlyDictionary<string, string> TerminalEnvironment { get; init; } = new Dictionary<string, string>();
}
