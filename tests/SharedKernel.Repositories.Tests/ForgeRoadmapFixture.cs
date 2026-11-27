namespace SharedKernel.Repositories.Tests;

/// <summary>A deterministic in-memory forge issue reader.</summary>
internal sealed class ForgeRoadmapFixture(
    ForgeIssueCapabilities capabilities,
    IReadOnlyList<ForgeIssue> openIssues,
    IReadOnlyList<ForgeIssue>? otherIssues = null) : IForgeIssueReader
{
    /// <inheritdoc />
    public ForgeIssueCapabilities Capabilities => capabilities;

    /// <inheritdoc />
    public Task<IReadOnlyList<ForgeIssue>> ReadOpenIssues(string repository, CancellationToken cancellationToken) =>
        Task.FromResult(openIssues);

    /// <inheritdoc />
    public Task<ForgeIssue> ReadIssue(ForgeIssueReference reference, CancellationToken cancellationToken) =>
        Task.FromResult((otherIssues ?? []).Single(issue => issue.Reference == reference));

    /// <summary>Creates a synthetic reference within the primary test repository.</summary>
    public static ForgeIssueReference Reference(long number) => new("example/planning", number);

    /// <summary>Creates an open synthetic issue within the primary test repository.</summary>
    public static ForgeIssue Issue(long number) => new(Reference(number), "Example issue", true, null, [], null, []);
}
