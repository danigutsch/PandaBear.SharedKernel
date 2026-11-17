using PandaBear.Processes;

namespace PandaBear.Secrets.Tests;

/// <summary>An <see cref="IExecutableResolver"/> that knows a fixed set of commands.</summary>
internal sealed class FixedExecutableResolver(IReadOnlyDictionary<string, string> commands) : IExecutableResolver
{
    /// <inheritdoc />
    public string? Find(string command) => commands.TryGetValue(command, out var path) ? path : null;
}
