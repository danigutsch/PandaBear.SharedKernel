using PandaBear.Processes;

namespace PandaBear.Testing;

/// <summary>
/// An <see cref="IExecutableResolver"/> that finds the executables a test maps, to the paths it maps them to, instead of
/// searching the <c>PATH</c>. A command that is not mapped goes to the fallback, when there is one, or is not found.
/// </summary>
/// <remarks>Map every command before the code under test resolves one; <see cref="Set"/> is not safe to call during lookups.</remarks>
public sealed class ExecutableMap : IExecutableResolver
{
    private readonly Dictionary<string, string?> _paths = new(StringComparer.Ordinal);
    private readonly Func<string, string?>? _fallback;

    /// <summary>Creates a map that finds no executable until one is set.</summary>
    public ExecutableMap()
    {
    }

    /// <summary>Creates a map that finds the given executables.</summary>
    /// <param name="executables">The executable path of each command name.</param>
    /// <exception cref="ArgumentNullException"><paramref name="executables"/> is <see langword="null"/>.</exception>
    public ExecutableMap(IEnumerable<KeyValuePair<string, string>> executables)
    {
        ArgumentNullException.ThrowIfNull(executables);
        foreach (var (command, path) in executables)
        {
            Set(command, path);
        }
    }

    /// <summary>Creates a map that answers every command that is not set with <paramref name="fallback"/>.</summary>
    /// <param name="fallback">Returns the path of a command that is not set, or <see langword="null"/> when it is not found.</param>
    /// <exception cref="ArgumentNullException"><paramref name="fallback"/> is <see langword="null"/>.</exception>
    public ExecutableMap(Func<string, string?> fallback)
    {
        ArgumentNullException.ThrowIfNull(fallback);
        _fallback = fallback;
    }

    /// <summary>Maps <paramref name="command"/> to <paramref name="path"/>, or with <see langword="null"/> makes it not found, fallback included.</summary>
    /// <param name="command">The command name.</param>
    /// <param name="path">The executable path, or <see langword="null"/> for a command that is not installed.</param>
    /// <exception cref="ArgumentException"><paramref name="command"/> is <see langword="null"/>, empty, or white space.</exception>
    public void Set(string command, string? path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        _paths[command] = path;
    }

    /// <inheritdoc />
    public string? Find(string command)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        return _paths.TryGetValue(command, out var path) ? path : _fallback?.Invoke(command);
    }
}
