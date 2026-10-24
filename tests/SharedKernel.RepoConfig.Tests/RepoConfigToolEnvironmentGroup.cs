namespace PandaBear.RepoConfig.Tests;

[SerialTestJustification("Repo config tool tests temporarily override process-wide GitHub token environment variables.")]
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class RepoConfigToolEnvironmentGroup
{
    /// <summary>
    /// Identifies tests that share process-wide tool environment variables.
    /// </summary>
    public const string Name = "Repo config tool environment";
}
