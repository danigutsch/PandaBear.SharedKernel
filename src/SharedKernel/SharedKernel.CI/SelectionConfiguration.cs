namespace SharedKernel.CI;

/// <summary>The path rules of <c>select</c>.</summary>
/// <param name="FullValidation">Path patterns whose change selects every test project.</param>
/// <param name="Documentation">Path patterns whose change selects nothing while every tracked build file is inert.</param>
/// <param name="AlwaysSelected">Test projects that run whenever a change selects any project.</param>
/// <param name="RequireCoveringTests">Whether a changed project that no selected test project depends on selects every test project.</param>
/// <param name="TestPaths">Rules that tie paths no project owns to the test projects that read them.</param>
public sealed record SelectionConfiguration(
    IReadOnlyList<string> FullValidation,
    IReadOnlyList<string> Documentation,
    IReadOnlyList<string> AlwaysSelected,
    bool RequireCoveringTests,
    IReadOnlyList<TestPathRule>? TestPaths = null);
