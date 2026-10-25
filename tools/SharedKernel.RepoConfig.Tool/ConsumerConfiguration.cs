namespace PandaBear.RepoConfig.Tool;

/// <summary>
/// The consumer policy in <c>.config/sharedkernel-repo.json</c>.
/// </summary>
/// <param name="Version">Format version; only <c>1</c> is supported.</param>
/// <param name="LicenseExceptions">The license exceptions that <c>dependency verify-license-exceptions</c> verifies, if any.</param>
/// <param name="Links">The documentation files that <c>verify-links</c> scans, if any.</param>
/// <param name="Gherkin">The feature files and rules that <c>verify-gherkin</c> checks, if any.</param>
/// <param name="Labels">The repository labels that <c>github sync-labels</c> creates and updates, if any.</param>
internal sealed record ConsumerConfiguration(
    int Version,
    LicenseExceptionConfiguration? LicenseExceptions = null,
    LinkConfiguration? Links = null,
    GherkinConfiguration? Gherkin = null,
    IReadOnlyList<GitHubLabelConfiguration>? Labels = null);
