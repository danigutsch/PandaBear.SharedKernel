namespace PandaBear.Repositories;

/// <summary>A test project and the seams it exercises.</summary>
/// <param name="Path">The tracked test project path relative to the repository root.</param>
/// <param name="Register">The tracked Markdown file documenting the seams.</param>
/// <param name="Seams">The explicitly declared SUT and type pairs exercised by this project.</param>
/// <param name="MigrationException">The documented reason for retaining a legacy name or mixed project.</param>
internal sealed record TestTaxonomySuite(
    string Path,
    string Register,
    TestTaxonomySeam[] Seams,
    string? MigrationException = null);
