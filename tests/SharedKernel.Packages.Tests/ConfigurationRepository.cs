namespace PandaBear.Packages.Tests;

/// <summary>Creates temporary repositories with a package configuration.</summary>
internal static class ConfigurationRepository
{
    /// <summary>Writes <paramref name="configuration"/> as <c>.config/sharedkernel-packages.json</c> and returns the directory.</summary>
    public static TemporaryDirectory Create(string configuration)
    {
        var directory = new TemporaryDirectory();
        Directory.CreateDirectory(Path.Combine(directory.Path, ".config"));
        File.WriteAllText(Path.Combine(directory.Path, ".config", "sharedkernel-packages.json"), configuration);
        return directory;
    }
}
