namespace PandaBear.CI.Tool;

/// <summary>A CI test slice: one matrix job that runs the selected test projects it lists.</summary>
/// <param name="Name">Lowercase letters and digits in hyphen-separated words; it names the job, the filter, and the output.</param>
/// <param name="Projects">The repository-relative test projects of the slice; only the default slice may list none.</param>
/// <param name="OpenApi">The OpenAPI targets that the slice generates before its tests run.</param>
/// <param name="Playwright">Whether the slice installs Playwright browsers before its tests run.</param>
internal sealed record SliceConfiguration(string Name, string[] Projects, string[]? OpenApi = null, bool Playwright = false);
