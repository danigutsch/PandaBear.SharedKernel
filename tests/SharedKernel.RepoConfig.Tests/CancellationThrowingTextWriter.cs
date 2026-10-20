namespace SharedKernel.RepoConfig.Tests;

internal sealed class CancellationThrowingTextWriter : StringWriter
{
    public override Task WriteLineAsync(ReadOnlyMemory<char> buffer, CancellationToken cancellationToken = default) =>
        Task.FromException(new OperationCanceledException(cancellationToken));
}
