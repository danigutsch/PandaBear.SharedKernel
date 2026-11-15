namespace PandaBear.Sandbox.Tests;

/// <summary>Creates a temporary repository with a sandbox configuration file.</summary>
internal static class ConfigurationRepository
{
    /// <summary>Creates the repository with <paramref name="json"/> as <c>.config/sharedkernel-sandbox.json</c>.</summary>
    public static TemporaryDirectory Create(string json)
    {
        var root = new TemporaryDirectory();
        Directory.CreateDirectory(Path.Combine(root.Path, ".config"));
        File.WriteAllText(Path.Combine(root.Path, SandboxConfigurationFile.RelativePath), json);
        return root;
    }
}
