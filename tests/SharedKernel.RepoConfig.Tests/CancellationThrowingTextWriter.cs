namespace PandaBear.RepoConfig.Tests;

/// <summary>A string writer whose asynchronous line write always fails with an <see cref="OperationCanceledException"/>.</summary>
internal sealed class CancellationThrowingTextWriter : StringWriter
{
    public override Task WriteLineAsync(ReadOnlyMemory<char> buffer, CancellationToken cancellationToken = default) =>
        Task.FromException(new OperationCanceledException(cancellationToken));
}
