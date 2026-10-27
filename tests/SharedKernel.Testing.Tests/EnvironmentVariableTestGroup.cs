namespace PandaBear.Testing.Tests;

[CollectionDefinition("Environment variables", DisableParallelization = true)]
[SerialTestJustification("Environment variables are process-wide mutable state.")]
public sealed class EnvironmentVariableTestGroup;
