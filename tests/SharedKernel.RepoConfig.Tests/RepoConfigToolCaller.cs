namespace PandaBear.RepoConfig.Tests;

/// <summary>A repo-config tool command line found in a workflow file.</summary>
/// <param name="File">The workflow file path, relative to the repository root, that contains the command line.</param>
/// <param name="Arguments">The arguments that follow the tool invocation, parsed from the workflow run step.</param>
internal sealed record RepoConfigToolCaller(string File, string[] Arguments);
