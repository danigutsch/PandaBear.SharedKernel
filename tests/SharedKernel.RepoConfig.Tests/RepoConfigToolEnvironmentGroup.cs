namespace SharedKernel.RepoConfig.Tests;

[SerialTestJustification("Repo config tool tests temporarily override process-wide GitHub token environment variables.")]
[CollectionDefinition("Repo config tool environment", DisableParallelization = true)]
public sealed class RepoConfigToolEnvironmentGroup
{
}
