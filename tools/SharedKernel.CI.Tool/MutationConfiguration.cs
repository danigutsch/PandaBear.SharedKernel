namespace PandaBear.CI.Tool;

/// <summary>The Stryker.NET runs of <c>run-mutation-tests</c>.</summary>
/// <param name="StrykerVersion">The exact <c>dotnet-stryker</c> package version that the command installs and runs.</param>
/// <param name="ConfigFiles">Repository-relative Stryker config files, run in order from their directories.</param>
internal sealed record MutationConfiguration(string StrykerVersion, string[] ConfigFiles);
