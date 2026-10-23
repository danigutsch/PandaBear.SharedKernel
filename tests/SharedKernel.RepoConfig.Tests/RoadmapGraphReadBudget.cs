namespace PandaBear.RepoConfig.Tests;

internal sealed class RoadmapGraphReadBudget(int maximumReads)
{
    public int Reads
    {
        get; private set;
    }

    public void RecordRead()
    {
        if (++Reads > maximumReads)
        {
            throw new InvalidOperationException("Graph traversal exceeded its linear edge budget.");
        }
    }
}
