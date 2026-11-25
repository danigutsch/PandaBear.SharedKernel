namespace SharedKernel.Repositories;

/// <summary>Identifies the forge whose paths and workflow syntax a repository uses.</summary>
public enum ForgeKind
{
    /// <summary>GitHub repository files and Actions syntax.</summary>
    GitHub,

    /// <summary>Forgejo repository files and Actions syntax.</summary>
    Forgejo
}
