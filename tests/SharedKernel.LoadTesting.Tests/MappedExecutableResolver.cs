namespace PandaBear.LoadTesting.Tests;

/// <summary>Resolves only the executables that a test maps, to the paths it maps them to.</summary>
internal sealed class MappedExecutableResolver : IExecutableResolver
{
    private readonly Dictionary<string, string> _executables = new(StringComparer.Ordinal);

    /// <summary>Maps a command name to an executable path.</summary>
    public void Set(string command, string path) => _executables[command] = path;

    /// <inheritdoc />
    public string? Find(string command) => _executables.GetValueOrDefault(command);
}
