namespace PandaBear.RepoConfig.Tests;

/// <summary>Creates workspaces with an action pin manifest, a workflow, and an optional composite action.</summary>
internal static class ActionPinTestWorkspace
{
    /// <summary>Creates a workspace whose manifest, <c>ci.yml</c> workflow, and local composite action hold the given content.</summary>
    public static TemporaryRepoConfigWorkspace Create(string manifest, string workflow, string? compositeAction)
    {
        var workspace = new TemporaryRepoConfigWorkspace();
        workspace.WriteFile(".github/action-pins.json", manifest);
        workspace.WriteFile(".github/workflows/ci.yml", workflow);
        if (compositeAction is not null)
        {
            workspace.WriteFile(".github/actions/local/action.yml", compositeAction);
        }

        return workspace;
    }
}
