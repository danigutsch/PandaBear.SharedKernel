namespace PandaBear.RepoConfig.Tests;

/// <summary>A stream whose asynchronous reads fail with an <see cref="IOException"/> whose message contains a token-like secret.</summary>
internal sealed class ResponseReadFailureStream : MemoryStream
{
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        ValueTask.FromException<int>(new IOException("ghp_untrusted_response_stream"));
}
