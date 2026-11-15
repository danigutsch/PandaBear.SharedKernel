namespace PandaBear.PlaywrightMcp.Tests;

/// <summary>Fake environment variables and installed executables for resolving <see cref="PlaywrightMcpSettings"/>.</summary>
internal sealed class FakeMachine : IExecutableResolver
{
    private readonly Dictionary<string, string?> _environment = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string?> _executables = new(StringComparer.Ordinal);

    /// <summary>Gets the settings that the environment variables set so far spell.</summary>
    public PlaywrightMcpSettings Settings => PlaywrightMcpSettings.FromEnvironment(name => _environment.GetValueOrDefault(name));

    /// <summary>Returns an absolute path under the temporary directory for a fake executable, with <c>.exe</c> on Windows.</summary>
    public static string GetExecutablePath(string name)
    {
        var fileName = OperatingSystem.IsWindows() ? $"{name}.exe" : name;
        return Path.GetFullPath(Path.Combine(Path.GetTempPath(), "playwright-mcp-tests", fileName));
    }

    /// <inheritdoc />
    public string? Find(string command)
    {
        return _executables.GetValueOrDefault(command);
    }

    /// <summary>Sets, or with <c>null</c> clears, an environment variable.</summary>
    public void SetEnvironmentVariable(string name, string? value)
    {
        _environment[name] = value;
    }

    /// <summary>Sets, or with <c>null</c> clears, the path an executable name resolves to.</summary>
    public void SetExecutable(string name, string? path)
    {
        _executables[name] = path;
    }
}
