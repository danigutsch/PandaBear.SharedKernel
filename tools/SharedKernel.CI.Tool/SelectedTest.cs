namespace PandaBear.CI.Tool;

/// <summary>A selected test project and what selected it.</summary>
/// <param name="Project">The repository-relative test project.</param>
/// <param name="SelectedBy">
/// The changed paths whose owning projects it depends on or is, and <c>alwaysSelected</c> when the configuration always
/// selects it; empty for full validation, whose reason the plan records.
/// </param>
internal sealed record SelectedTest(string Project, string[] SelectedBy);
