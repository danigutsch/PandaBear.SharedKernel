namespace SharedKernel.Repositories;

/// <summary>Identifies an issue linked by the forge and carries its reported state.</summary>
/// <param name="Reference">The linked issue.</param>
/// <param name="IsOpen">Whether the linked issue is open.</param>
public sealed record ForgeIssueLink(ForgeIssueReference Reference, bool IsOpen);
