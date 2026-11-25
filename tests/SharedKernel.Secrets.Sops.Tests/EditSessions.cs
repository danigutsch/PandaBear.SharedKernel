namespace SharedKernel.Secrets.Sops.Tests;

/// <summary>Builds edit sessions for tests.</summary>
internal static class EditSessions
{
    /// <summary>A session whose editor is the test host, which exists on every platform, in <paramref name="privateDirectory"/>.</summary>
    public static SopsEditSession For(string privateDirectory) => new()
    {
        Editor = EditorCommand.Create(Environment.ProcessPath, new ExecutableMap()).ShouldBeSuccess(),
        PrivateDirectory = privateDirectory,
    };
}
