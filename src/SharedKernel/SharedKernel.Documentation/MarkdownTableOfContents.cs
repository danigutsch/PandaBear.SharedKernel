using SharedKernel.Markdown;

namespace SharedKernel.Documentation;

/// <summary>Renders a list of links to headings in a Markdown document.</summary>
internal static class MarkdownTableOfContents
{
    /// <summary>Builds a nested list for headings in the inclusive level range.</summary>
    public static string Build(string markdown, int minHeadingLevel, int maxHeadingLevel)
    {
        var source = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var output = new List<string>();
        var parents = new Stack<int>();
        foreach (var (level, title, anchor) in MarkdownHeadings.Read(source))
        {
            if (level < minHeadingLevel || level > maxHeadingLevel || anchor.Length == 0)
            {
                continue;
            }

            while (parents.TryPeek(out var parent) && parent >= level)
            {
                parents.Pop();
            }

            var indent = new string(' ', parents.Count * 2);
            var label = title.Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("[", "\\[", StringComparison.Ordinal)
                .Replace("]", "\\]", StringComparison.Ordinal);
            output.Add($"{indent}- [{label}](#{Uri.EscapeDataString(anchor)})");
            parents.Push(level);
        }

        return string.Join('\n', output);
    }
}
