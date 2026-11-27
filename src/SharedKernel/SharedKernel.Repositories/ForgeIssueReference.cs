namespace SharedKernel.Repositories;

/// <summary>Identifies an issue within one forge.</summary>
/// <param name="Repository">The issue repository in <c>owner/name</c> form.</param>
/// <param name="Number">The positive, repository-local issue number.</param>
public sealed record ForgeIssueReference(string Repository, long Number);
