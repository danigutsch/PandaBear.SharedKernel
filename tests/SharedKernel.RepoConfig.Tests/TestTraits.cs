namespace PandaBear.RepoConfig.Tests;

/// <summary>
/// Trait values shared by the RepoConfig test classes.
/// </summary>
internal static class TestTraits
{
    /// <summary>
    /// Trait value for tests that exercise the command-line entry point.
    /// </summary>
    public const string CommandLineCategory = "command-line";
    /// <summary>
    /// Trait value for tests that belong to the tooling area.
    /// </summary>
    public const string ToolingArea = "tooling";
    /// <summary>
    /// Trait value for unit-scope tests.
    /// </summary>
    public const string UnitScope = "unit";
}
