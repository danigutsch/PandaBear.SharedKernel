namespace PandaBear.RepoConfig.Tool;

/// <summary>Result of checking an issue's labels against the configured set.</summary>
/// <param name="ExtraLabels">Labels on the issue that are not configured.</param>
/// <param name="Diagnostic">Reason the check could not complete, or <see langword="null" />.</param>
internal sealed record GitHubIssueSyncCheck(IReadOnlyList<string> ExtraLabels, string? Diagnostic);
