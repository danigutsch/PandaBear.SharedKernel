namespace SharedKernel.Repositories;

/// <summary>Supplies optional caller-owned policy templates for a new repository.</summary>
/// <param name="LicenseSource">The source file for <c>LICENSE</c>.</param>
/// <param name="SecuritySource">The source file for <c>SECURITY.md</c>.</param>
/// <param name="CodeOwnersSource">The source file for <c>CODEOWNERS</c>.</param>
/// <param name="Owner">The value of the optional <c>@@owner@@</c> placeholder.</param>
/// <param name="CopyrightHolder">The value of the optional <c>@@copyrightHolder@@</c> placeholder.</param>
public sealed record RepositoryPolicySources(
    string? LicenseSource = null,
    string? SecuritySource = null,
    string? CodeOwnersSource = null,
    string? Owner = null,
    string? CopyrightHolder = null)
{
    /// <summary>Gets the optional forge for pull request and issue templates.</summary>
    public ForgeKind? Forge { get; init; }

    /// <summary>Gets the path to the optional caller-owned JSON policy for the selected forge's CI workflow.</summary>
    public string? CiPolicySource { get; init; }
}
