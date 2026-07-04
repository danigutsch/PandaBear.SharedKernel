namespace SharedKernel.Versioning;

/// <summary>
/// Represents a parsed Conventional Commit message.
/// </summary>
/// <param name="Type">The commit type.</param>
/// <param name="Scope">The optional scope.</param>
/// <param name="IsBreakingHeader">Whether the header contains a breaking-change bang marker.</param>
/// <param name="Description">The header description.</param>
/// <param name="Body">The optional body text.</param>
/// <param name="Footers">The parsed footer lines.</param>
public sealed record ConventionalCommit(
    string Type,
    string? Scope,
    bool IsBreakingHeader,
    string Description,
    string? Body,
    IReadOnlyList<string> Footers)
{
    /// <summary>
    /// Gets whether the commit contains a breaking-change marker.
    /// </summary>
    public bool IsBreakingChange => IsBreakingHeader || Footers.Any(IsBreakingChangeFooter);

    /// <summary>
    /// Gets the release impact described by the commit.
    /// </summary>
    public ReleaseImpact Impact
    {
        get
        {
            if (IsBreakingChange)
            {
                return ReleaseImpact.Major;
            }

            return Type switch
            {
                "feat" => ReleaseImpact.Minor,
                "fix" or "perf" => ReleaseImpact.Patch,
                _ => ReleaseImpact.None,
            };
        }
    }

    private static bool IsBreakingChangeFooter(string footer) =>
        footer.StartsWith("BREAKING CHANGE: ", StringComparison.Ordinal) ||
        footer.StartsWith("BREAKING-CHANGE: ", StringComparison.Ordinal);
}
