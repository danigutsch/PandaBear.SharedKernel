using PandaBear.DocumentWorkflow;
using PandaBear.Results;

namespace PandaBear.EntityFrameworkCore.Tests;

/// <summary>Records calls and returns the immutable handle for the test release document.</summary>
internal sealed class ReleaseDocumentArtifactStore : IDocumentArtifactStore
{
    private int writes;

    /// <summary>Gets how many write requests the store handled.</summary>
    public int Writes => Volatile.Read(ref writes);

    /// <inheritdoc />
    public ValueTask<Result<string>> Write(DocumentArtifactWrite artifact, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Interlocked.Increment(ref writes);
        return ValueTask.FromResult(Result.Ok("private/release-1"));
    }
}
