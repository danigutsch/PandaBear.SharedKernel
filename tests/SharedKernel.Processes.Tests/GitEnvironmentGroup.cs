namespace SharedKernel.Processes.Tests;

/// <summary>Serial xUnit collection for tests that put credentials and Git variables in the process environment.</summary>
[SerialTestJustification("The tests set process-wide token, askpass, and GIT_* variables that a concurrent git run would inherit.")]
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class GitEnvironmentGroup
{
    /// <summary>Identifies tests that change the process environment that git inherits.</summary>
    public const string Name = "Git environment";
}
