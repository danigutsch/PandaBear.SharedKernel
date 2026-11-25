namespace PandaBear.Repositories;

/// <summary>The actual SUT, test type, dependencies, capabilities, and CI lane for a seam.</summary>
/// <param name="Sut">The project under test, as registered in the inventory.</param>
/// <param name="Type">The test type at this seam.</param>
/// <param name="Dependency">The dependency boundary of the seam.</param>
/// <param name="Capabilities">The behaviors directly exercised by the seam.</param>
/// <param name="Lane">The CI lane that runs the tests.</param>
internal sealed record TestTaxonomySeam(
    string Sut,
    string Type,
    string Dependency,
    string[] Capabilities,
    string Lane);
