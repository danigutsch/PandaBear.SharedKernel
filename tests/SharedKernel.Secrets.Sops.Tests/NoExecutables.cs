using PandaBear.Processes;

namespace PandaBear.Secrets.Sops.Tests;

/// <summary>An <see cref="IExecutableResolver"/> that finds nothing.</summary>
internal sealed class NoExecutables : IExecutableResolver
{
    /// <inheritdoc />
    public string? Find(string command) => null;
}
