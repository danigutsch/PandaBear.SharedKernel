namespace PandaBear.Packages.Tool.Tests;

/// <summary>An <see cref="IExecutableResolver"/> that answers with a function.</summary>
internal sealed class FakeExecutables(Func<string, string?> find) : IExecutableResolver
{
    /// <inheritdoc />
    public string? Find(string command) => find(command);
}
