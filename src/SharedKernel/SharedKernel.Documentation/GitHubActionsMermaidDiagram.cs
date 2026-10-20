using System.Text;
using System.Text.RegularExpressions;

namespace SharedKernel.Documentation;

/// <summary>
/// Builds Mermaid diagrams from simple GitHub Actions workflow structure.
/// </summary>
internal static partial class GitHubActionsMermaidDiagram
{
    private static readonly UTF8Encoding Utf8NoBom = new(false);

    /// <summary>
    /// Builds a job dependency diagram from one workflow file.
    /// </summary>
    public static string BuildJobs(string workflowPath, string triggerLabel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(triggerLabel);

        var jobs = ParseWorkflowJobs(workflowPath);
        var lines = new List<string> { $"    trigger[{triggerLabel}]" };
        foreach (var (jobId, name, _) in jobs)
        {
            lines.Add($"    {MermaidDiagram.NodeId(jobId)}[{name}]");
        }

        foreach (var (jobId, _, needs) in jobs)
        {
            if (needs.Count == 0)
            {
                lines.Add($"    trigger --> {MermaidDiagram.NodeId(jobId)}");
                continue;
            }

            lines.AddRange(needs.Select(need => $"    {MermaidDiagram.NodeId(need)} --> {MermaidDiagram.NodeId(jobId)}"));
        }

        return MermaidDiagram.Build(MermaidDiagram.TopBottom, lines);
    }

    /// <summary>
    /// Builds a workflow inventory diagram for workflow files in one directory.
    /// </summary>
    public static string BuildWorkflowInventory(string workflowsPath, string rootLabel, string excludedWorkflowFileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowsPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(rootLabel);
        ArgumentException.ThrowIfNullOrWhiteSpace(excludedWorkflowFileName);

        var lines = new List<string> { $"    repo[{rootLabel}]" };
        foreach (var workflow in new DirectoryInfo(workflowsPath).EnumerateFiles("*.yml").OrderBy(file => file.Name, StringComparer.Ordinal))
        {
            if (workflow.Name == excludedWorkflowFileName)
            {
                continue;
            }

            var nodeId = MermaidDiagram.NodeId(Path.GetFileNameWithoutExtension(workflow.Name));
            lines.Add($"    {nodeId}[{WorkflowName(workflow.FullName)}]");
            lines.Add($"    repo --> {nodeId}");
        }

        return MermaidDiagram.Build(MermaidDiagram.LeftRight, lines);
    }

    private static List<(string JobId, string Name, List<string> Needs)> ParseWorkflowJobs(string path)
    {
        var jobs = new List<(string JobId, string Name, List<string> Needs)>();
        var currentId = string.Empty;
        var currentName = string.Empty;
        var currentNeeds = new List<string>();
        var needsBlock = false;

        foreach (var line in WorkflowJobLines(path))
        {
            var jobMatch = JobHeaderRegex().Match(line);
            if (jobMatch.Success)
            {
                FlushWorkflowJob(jobs, currentId, currentName, currentNeeds);
                currentId = jobMatch.Groups[1].Value;
                currentName = string.Empty;
                currentNeeds = [];
                needsBlock = false;
                continue;
            }

            if (currentId.Length == 0)
            {
                continue;
            }

            var name = WorkflowValue(line, "name");
            if (name.Length > 0)
            {
                currentName = name.Trim('"');
                continue;
            }

            var needsInline = JobNeedsRegex().Match(line);
            if (needsInline.Success)
            {
                currentNeeds.Add(needsInline.Groups[1].Value);
                continue;
            }

            if (JobNeedsBlockRegex().IsMatch(line))
            {
                needsBlock = true;
                continue;
            }

            if (needsBlock)
            {
                needsBlock = AppendNeed(line, currentNeeds);
            }
        }

        FlushWorkflowJob(jobs, currentId, currentName, currentNeeds);
        return jobs;
    }

    private static void FlushWorkflowJob(List<(string JobId, string Name, List<string> Needs)> jobs, string jobId, string name, List<string> needs)
    {
        if (jobId.Length > 0)
        {
            var diagramName = name.Length == 0 || name.Contains("${{", StringComparison.Ordinal)
                ? jobId
                : name;
            jobs.Add((jobId, diagramName, needs));
        }
    }

    private static IEnumerable<string> WorkflowJobLines(string path)
    {
        var lines = File.ReadAllLines(path, Utf8NoBom);
        var jobsIndex = Array.IndexOf(lines, "jobs:");
        return jobsIndex < 0 ? [] : lines.Skip(jobsIndex + 1);
    }

    private static bool AppendNeed(string line, List<string> needs)
    {
        var match = JobNeedItemRegex().Match(line);
        if (!match.Success)
        {
            return false;
        }

        needs.Add(match.Groups[1].Value);
        return true;
    }

    private static string WorkflowName(string path)
    {
        foreach (var line in File.ReadLines(path, Utf8NoBom))
        {
            var name = WorkflowValue(line, "name");
            if (name.Length > 0)
            {
                return name.Trim('"');
            }
        }

        return Path.GetFileNameWithoutExtension(path);
    }

    private static string WorkflowValue(string line, string key)
    {
        var prefix = line.Length > 0 && !char.IsWhiteSpace(line[0]) ? $"{key}:" : $"    {key}:";
        return line.StartsWith(prefix, StringComparison.Ordinal) ? line[prefix.Length..].Trim() : string.Empty;
    }

    [GeneratedRegex(@"^ {2}(\w[\w-]*):\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex JobHeaderRegex();

    [GeneratedRegex(@"^ {4}needs:\s+(\w[\w-]*)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex JobNeedsRegex();

    [GeneratedRegex(@"^ {4}needs:\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex JobNeedsBlockRegex();

    [GeneratedRegex(@"^ {6}-\s+(\w[\w-]*)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex JobNeedItemRegex();
}
