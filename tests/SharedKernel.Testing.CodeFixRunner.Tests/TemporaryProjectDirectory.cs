namespace SharedKernel.Testing.CodeFixRunner.Tests;

internal sealed class TemporaryProjectDirectory : IDisposable
{
    private TemporaryProjectDirectory(string path)
    {
        Path = path;
    }

    public string Path { get; }

    public static TemporaryProjectDirectory Create()
    {
        return new TemporaryProjectDirectory(CodeFixRunnerTestProject.CreateTemporaryProject());
    }

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
