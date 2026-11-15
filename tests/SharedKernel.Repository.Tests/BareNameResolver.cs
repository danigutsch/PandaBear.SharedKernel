using PandaBear.Processes;

namespace PandaBear.Repository.Tests;

/// <summary>Resolves every command to its own name, so fake process runners see <c>git</c> and <c>dotnet</c> as the tool asks for them.</summary>
internal sealed class BareNameResolver : IExecutableResolver
{
    /// <summary>Gets the shared resolver.</summary>
    public static BareNameResolver Instance { get; } = new();

    /// <inheritdoc />
    public string? Find(string command) => command;
}
