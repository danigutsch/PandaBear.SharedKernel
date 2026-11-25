namespace PandaBear.Repositories;

/// <summary>A production project, its direct owner, and the types of tests it requires.</summary>
/// <param name="Path">The tracked project path relative to the repository root.</param>
/// <param name="DirectOwner">The group directly responsible for the project.</param>
/// <param name="RequiredTypes">The test types that must have declared seams.</param>
internal sealed record TestTaxonomySut(string Path, string DirectOwner, string[] RequiredTypes);
