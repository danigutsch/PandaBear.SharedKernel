namespace PandaBear.Repository.Tests;

/// <summary>Reads job blocks from a GitHub Actions workflow file.</summary>
internal static class WorkflowJobs
{
    /// <summary>Returns a job's lines, from its key under <c>jobs:</c> to the next line indented by fewer than four spaces.</summary>
    /// <exception cref="InvalidOperationException">The workflow has no job with that key.</exception>
    public static string Read(string workflow, string job)
    {
        var lines = workflow.Split('\n');
        var start = Array.IndexOf(lines, "  " + job + ":");
        if (start < 0)
        {
            throw new InvalidOperationException("The workflow has no job " + job + ".");
        }

        var end = start + 1;
        while (end < lines.Length && (lines[end].Length == 0 || lines[end].StartsWith("    ", StringComparison.Ordinal)))
        {
            end++;
        }

        return string.Join('\n', lines[start..end]) + "\n";
    }
}
