namespace PandaBear.Sandbox.Tool.Tests;

/// <summary>Names the xUnit collection of tests that read or change process-wide environment variables, so they do not run in parallel.</summary>
internal static class EnvironmentCollection
{
    /// <summary>The collection name.</summary>
    public const string Name = "SandboxEnvironment";
}
