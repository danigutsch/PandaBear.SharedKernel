namespace PandaBear.RepoConfig.Tests;

/// <summary>An atomic-write precondition that changes <c>input.json</c> on its first check and then fails because the file no longer matches the original.</summary>
/// <param name="workspace">The workspace that holds <c>input.json</c>.</param>
internal sealed class ChangingAtomicWritePrecondition(TemporaryRepoConfigWorkspace workspace)
{
    /// <summary>The number of times <see cref="Verify"/> has run.</summary>
    public int VerificationCount
    {
        get; private set;
    }

    /// <summary>Overwrites <c>input.json</c> on the first call and throws on later calls because the file differs from its original content.</summary>
    public void Verify()
    {
        VerificationCount++;
        if (VerificationCount == 1)
        {
            workspace.WriteFile("input.json", "concurrent-input");
            return;
        }

        if (!string.Equals(workspace.ReadFile("input.json"), "original-input", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("File changed after the write plan was created: input.json.");
        }
    }
}
