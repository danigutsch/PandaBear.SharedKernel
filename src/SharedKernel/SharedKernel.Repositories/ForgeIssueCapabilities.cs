namespace SharedKernel.Repositories;

/// <summary>Optional metadata a forge can supply for a roadmap issue.</summary>
[Flags]
public enum ForgeIssueCapabilities
{
    /// <summary>The provider supplies issue state and labels.</summary>
    None = 0,

    /// <summary>The provider supplies reasons for issue state changes.</summary>
    StateReason = 1,

    /// <summary>The provider supplies parent issue relationships.</summary>
    Parent = 2,

    /// <summary>The provider supplies blocker relationships.</summary>
    Blockers = 4
}
