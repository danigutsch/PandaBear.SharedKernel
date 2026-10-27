namespace PandaBear.RepoConfig.Tests;

/// <summary>Counts blocker or parent reads during a graph traversal and fails once the count passes a limit, to prove traversal is linear.</summary>
/// <param name="maximumReads">The most reads allowed before <see cref="RecordRead"/> throws.</param>
internal sealed class RoadmapGraphReadBudget(int maximumReads)
{
    /// <summary>Gets the number of reads recorded so far.</summary>
    public int Reads
    {
        get; private set;
    }

    /// <summary>Counts one read and throws <see cref="InvalidOperationException"/> when the count exceeds the maximum.</summary>
    public void RecordRead()
    {
        if (++Reads > maximumReads)
        {
            throw new InvalidOperationException("Graph traversal exceeded its linear edge budget.");
        }
    }
}
