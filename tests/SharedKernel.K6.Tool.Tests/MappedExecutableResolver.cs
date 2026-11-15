namespace PandaBear.K6.Tool.Tests;

/// <summary>Resolves only the executables that a test maps, to the paths it maps them to.</summary>
/// <param name="executables">The executable path of each command name.</param>
internal sealed class MappedExecutableResolver(IReadOnlyDictionary<string, string> executables) : IExecutableResolver
{
    /// <inheritdoc />
    public string? Find(string command) => executables.GetValueOrDefault(command);
}
