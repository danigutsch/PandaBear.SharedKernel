namespace SharedKernel.Repositories.GitHub;

/// <summary>Caller-owned policy for verifying a GitHub Project.</summary>
/// <param name="Version">The configuration format version.</param>
/// <param name="Target">The Project identity.</param>
/// <param name="Fields">Required fields and their types.</param>
/// <param name="Issues">Issues whose membership and fields are required.</param>
internal sealed record GitHubProjectTaxonomyConfiguration(
    int Version,
    GitHubProjectTarget Target,
    GitHubProjectTaxonomyConfiguration.Field?[] Fields,
    GitHubProjectTaxonomyConfiguration.Issue?[] Issues)
{
    /// <summary>A required Project field.</summary>
    /// <param name="Name">The exact field name.</param>
    /// <param name="Type">The GitHub Project field data type.</param>
    internal sealed record Field(string Name, string Type);

    /// <summary>A repository-qualified issue number.</summary>
    /// <param name="Repository">The GitHub repository as <c>owner/name</c>.</param>
    /// <param name="Number">The issue number in that repository.</param>
    internal sealed record Issue(string Repository, int Number);
}
