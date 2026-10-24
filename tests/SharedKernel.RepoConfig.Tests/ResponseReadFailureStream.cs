namespace PandaBear.RepoConfig.Tests;

internal sealed class ResponseReadFailureStream : MemoryStream
{
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        ValueTask.FromException<int>(new IOException("ghp_untrusted_response_stream"));
}
