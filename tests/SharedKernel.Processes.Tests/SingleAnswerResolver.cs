namespace PandaBear.Processes.Tests;

/// <summary>An <see cref="IExecutableResolver"/> that gives the same answer for every command.</summary>
/// <param name="path">The answer, which may be <see langword="null"/> or not fully qualified.</param>
internal sealed class SingleAnswerResolver(string? path) : IExecutableResolver
{
    /// <inheritdoc />
    public string? Find(string command) => path;
}
