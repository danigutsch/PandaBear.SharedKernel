using System.Xml.Linq;

namespace SharedKernel.Documentation;

/// <summary>
/// Builds Mermaid diagrams from SDK-style project references.
/// </summary>
internal static class ProjectReferenceMermaidDiagram
{
    /// <summary>
    /// Builds a project dependency diagram for projects accepted by <paramref name="includeProject" />.
    /// </summary>
    public static string Build(string rootPath, string sourceRelativePath, Func<string[], bool> includeProject)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceRelativePath);
        ArgumentNullException.ThrowIfNull(includeProject);

        var edges = ProjectReferenceEdges(rootPath, sourceRelativePath, includeProject);
        return MermaidDiagram.Build(MermaidDiagram.TopBottom, MermaidDiagram.FormatEdges(edges));
    }

    private static List<(string Source, string Target)> ProjectReferenceEdges(string rootPath, string sourceRelativePath, Func<string[], bool> includeProject)
    {
        var edges = new List<(string Source, string Target)>();
        foreach (var project in new DirectoryInfo(Path.Combine(rootPath, sourceRelativePath)).EnumerateFiles("*.csproj", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(rootPath, project.FullName).Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            var relativeParts = relativePath.Split(Path.DirectorySeparatorChar);
            if (!includeProject(relativeParts))
            {
                continue;
            }

            var xmlRoot = XDocument.Load(project.FullName).Root;
            if (xmlRoot is null)
            {
                continue;
            }

            foreach (var reference in xmlRoot.Descendants("ProjectReference"))
            {
                var include = reference.Attribute("Include")?.Value;
                if (string.IsNullOrWhiteSpace(include))
                {
                    continue;
                }

                var targetPath = Path.GetFullPath(include.Replace('\\', Path.DirectorySeparatorChar), project.DirectoryName ?? rootPath);
                edges.Add((Path.GetFileNameWithoutExtension(project.Name), Path.GetFileNameWithoutExtension(targetPath)));
            }
        }

        return edges.Distinct().OrderBy(edge => edge.Source, StringComparer.Ordinal).ThenBy(edge => edge.Target, StringComparer.Ordinal).ToList();
    }
}
