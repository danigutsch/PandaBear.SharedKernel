namespace PandaBear.K6.Tool;

/// <summary>The consumer policy in <c>.config/sharedkernel-k6.json</c>.</summary>
/// <param name="Version">Format version; only <c>1</c> is supported.</param>
/// <param name="ScriptDirectory">The repository-relative directory that holds the k6 scripts; containers mount it read-only.</param>
/// <param name="ResultsDirectory">The repository-relative directory under which each run writes its own directory.</param>
/// <param name="Scenarios">The scenarios that <c>run --scenario</c> selects by name.</param>
/// <param name="Container">The engine and default image of <c>run --container</c>.</param>
internal sealed record K6Configuration(
    int Version,
    string ScriptDirectory,
    string ResultsDirectory,
    K6ScenarioConfiguration[] Scenarios,
    K6ContainerConfiguration? Container = null);
