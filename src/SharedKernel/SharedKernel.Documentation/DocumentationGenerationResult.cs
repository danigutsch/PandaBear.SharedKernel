namespace SharedKernel.Documentation;

/// <summary>
/// Result of a generated documentation update or drift check.
/// </summary>
public sealed class DocumentationGenerationResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentationGenerationResult" /> class.
    /// </summary>
    public DocumentationGenerationResult(IReadOnlyList<string> changedFiles)
    {
        ArgumentNullException.ThrowIfNull(changedFiles);

        ChangedFiles = changedFiles;
    }

    /// <summary>
    /// Gets repository-relative file paths changed or stale.
    /// </summary>
    public IReadOnlyList<string> ChangedFiles { get; }
}
