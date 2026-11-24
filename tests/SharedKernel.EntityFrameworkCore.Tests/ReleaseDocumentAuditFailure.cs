namespace PandaBear.EntityFrameworkCore.Tests;

/// <summary>Injects one failure into transactional audit storage.</summary>
internal sealed class ReleaseDocumentAuditFailure
{
    private int fail;

    /// <summary>Arranges a failure on the next check.</summary>
    public void Arm() => Interlocked.Exchange(ref fail, 1);

    /// <summary>Throws and clears the pending failure when armed.</summary>
    public void Check()
    {
        if (Interlocked.Exchange(ref fail, 0) == 1)
        {
            throw new InvalidOperationException("Simulated audit write failure.");
        }
    }
}
