namespace PandaBear.Testing.Tests;

/// <summary>Collection definition that runs tests touching process-wide environment variables serially.</summary>
[CollectionDefinition("Environment variables", DisableParallelization = true)]
[SerialTestJustification("Environment variables are process-wide mutable state.")]
public sealed class EnvironmentVariableTestGroup;
