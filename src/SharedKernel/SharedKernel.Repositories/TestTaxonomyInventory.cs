namespace PandaBear.Repositories;

/// <summary>The tracked project inventory and its production and test directories.</summary>
/// <param name="Version">The inventory schema version.</param>
/// <param name="SutRoots">Roots of tracked production projects.</param>
/// <param name="TestRoots">Roots of tracked test and test-support projects.</param>
/// <param name="Suts">The production projects and their owners.</param>
/// <param name="Suites">The runnable test projects and their seams.</param>
/// <param name="SupportProjects">The test-only hosts, contracts, and helpers that do not run tests.</param>
internal sealed record TestTaxonomyInventory(
    int Version,
    string[] SutRoots,
    string[] TestRoots,
    TestTaxonomySut[] Suts,
    TestTaxonomySuite[] Suites,
    string[] SupportProjects);
