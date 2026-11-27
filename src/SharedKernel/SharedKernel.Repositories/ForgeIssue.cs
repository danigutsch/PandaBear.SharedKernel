namespace SharedKernel.Repositories;

/// <summary>The issue metadata a forge supplies to a roadmap.</summary>
/// <param name="Reference">The repository and issue number.</param>
/// <param name="Title">The issue title.</param>
/// <param name="IsOpen">Whether the issue is open.</param>
/// <param name="StateReason">The forge's reason for the current state, when supplied.</param>
/// <param name="Labels">The names of the labels applied to this issue.</param>
/// <param name="Parent">The parent link reported by the forge, when present.</param>
/// <param name="BlockedBy">The blocker links reported by the forge.</param>
public sealed record ForgeIssue(
    ForgeIssueReference Reference,
    string Title,
    bool IsOpen,
    string? StateReason,
    IReadOnlyList<string> Labels,
    ForgeIssueLink? Parent,
    IReadOnlyList<ForgeIssueLink> BlockedBy);
