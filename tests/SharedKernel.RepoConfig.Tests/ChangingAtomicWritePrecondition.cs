namespace SharedKernel.RepoConfig.Tests;

internal sealed class ChangingAtomicWritePrecondition(TemporaryRepoConfigWorkspace workspace)
{
    public int VerificationCount { get; private set; }

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
