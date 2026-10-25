namespace PandaBear.RepoConfig.Tool;

/// <summary>
/// A repository label that <c>github sync-labels</c> creates or updates.
/// </summary>
/// <param name="Name">Label name; GitHub matches label names ignoring case.</param>
/// <param name="Color">Six hexadecimal digits without <c>#</c>.</param>
/// <param name="Description">Label description, which may be empty.</param>
internal sealed record GitHubLabelConfiguration(string Name, string Color, string Description);
