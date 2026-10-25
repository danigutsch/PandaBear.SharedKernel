namespace PandaBear.CI.Tool;

/// <summary>A rule of <c>selection.testPaths</c> that ties paths no project owns to the test projects that read them.</summary>
/// <param name="Paths">Path patterns whose change selects <paramref name="Tests"/>.</param>
/// <param name="Tests">The solution test projects that a change of a matching path selects.</param>
internal sealed record TestPathRule(string[] Paths, string[] Tests);
