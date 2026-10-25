namespace PandaBear.CI.Tool;

/// <summary>What <c>run</c> runs and how.</summary>
/// <param name="Slices">The slices to run; none runs every required slice of the plan.</param>
/// <param name="Configuration">The build configuration.</param>
/// <param name="Coverage">Whether to collect Cobertura coverage per test project and require a report from each.</param>
/// <param name="InstallPlaywright">Whether to install the Playwright browsers that the selected slices configure.</param>
/// <param name="PlaywrightDependencies">Whether to also install the browsers' operating system dependencies.</param>
/// <param name="ExcludeTraits">The <c>name=value</c> traits whose tests are left out of the test run, each as a <c>--filter-not-trait</c> argument.</param>
/// <param name="GithubSummary">The GitHub Actions step summary file that receives the phases, or <see langword="null"/>.</param>
internal sealed record SliceRunOptions(
    string[] Slices,
    string Configuration,
    bool Coverage,
    bool InstallPlaywright,
    bool PlaywrightDependencies,
    string[] ExcludeTraits,
    string? GithubSummary);
