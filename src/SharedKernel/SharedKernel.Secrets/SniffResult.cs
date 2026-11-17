namespace PandaBear.Secrets;

/// <summary>What a look at a file's content found.</summary>
internal enum SniffResult
{
    /// <summary>The file is positively not a SOPS document.</summary>
    NotSops,

    /// <summary>The file has SOPS metadata.</summary>
    Sops,

    /// <summary>The file cannot be ruled out: binary, not UTF-8, or JSON-looking text that does not parse.</summary>
    Unknown,
}
