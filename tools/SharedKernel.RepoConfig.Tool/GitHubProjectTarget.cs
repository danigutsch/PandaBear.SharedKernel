namespace PandaBear.RepoConfig.Tool;

/// <summary>GitHub project that roadmap items are synchronized to.</summary>
/// <param name="Id">Project node ID.</param>
/// <param name="Owner">Login of the project owner.</param>
/// <param name="Number">Project number.</param>
internal sealed record GitHubProjectTarget(string Id, string Owner, int Number);
