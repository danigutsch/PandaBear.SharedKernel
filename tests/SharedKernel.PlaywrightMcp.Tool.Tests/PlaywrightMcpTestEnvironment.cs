namespace SharedKernel.PlaywrightMcp.Tool.Tests;

internal sealed class PlaywrightMcpTestEnvironment
{
    private readonly Dictionary<string, string?> _environment = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string?> _executables = new(StringComparer.Ordinal);

    public string? GetEnvironmentVariable(string name)
    {
        return _environment.GetValueOrDefault(name);
    }

    public static string GetExecutablePath(string name)
    {
        var fileName = OperatingSystem.IsWindows() ? $"{name}.exe" : name;
        return Path.GetFullPath(Path.Combine(Path.GetTempPath(), "playwright-mcp-tests", fileName));
    }

    public string? ResolveExecutable(string name)
    {
        return _executables.GetValueOrDefault(name);
    }

    public void SetEnvironmentVariable(string name, string? value)
    {
        _environment[name] = value;
    }

    public void SetExecutable(string name, string? path)
    {
        _executables[name] = path;
    }
}
