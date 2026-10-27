using PandaBear.Testing;

namespace PandaBear.AspNetCore.Tests;

/// <summary>
/// Test collection of every test that replaces the process-wide standard error. A collection that disables parallelization runs
/// after the parallel collections have finished and alone, so nothing else writes to standard error while it is redirected.
/// </summary>
[SerialTestJustification("Console.SetError replaces the process-wide standard error, so no other test may write to it meanwhile.")]
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SerialConsoleError
{
    /// <summary>The collection name.</summary>
    public const string Name = "Console.Error";
}
