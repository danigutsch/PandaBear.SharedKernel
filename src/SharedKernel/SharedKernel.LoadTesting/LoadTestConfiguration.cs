namespace PandaBear.LoadTesting;

/// <summary>The consumer policy in the load-test configuration file.</summary>
/// <param name="Version">Format version; only <c>1</c> is supported.</param>
/// <param name="ScriptDirectory">The repository-relative directory that holds the scripts; containers mount it read-only.</param>
/// <param name="ResultsDirectory">The repository-relative directory under which each run writes its own directory.</param>
/// <param name="Scenarios">The scenarios that a run selects by name.</param>
/// <param name="Container">The engine and default image of a container run.</param>
internal sealed record LoadTestConfiguration(
    int Version,
    string ScriptDirectory,
    string ResultsDirectory,
    ScenarioConfiguration[] Scenarios,
    ContainerConfiguration? Container = null);
