using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace PandaBear.Repository.Tool;

internal static class ProjectSelector
{
    public static ProjectSelection Select(string root, IEnumerable<string>? changes)
    {
        var solution = XDocument.Load(Path.Combine(root, "SharedKernel.slnx"));
        var projects = solution.Descendants("Project").Select(x => (string)x.Attribute("Path")!).Order(StringComparer.Ordinal).ToArray();
        var documents = projects.ToDictionary(p => p, p => XDocument.Load(Path.Combine(root, p)), StringComparer.Ordinal);
        var tests = projects.Where(p => documents[p].Descendants().Where(static element => element.Name.LocalName == "PackageReference").Any(x => (string?)x.Attribute("Include") == "xunit.v3.mtp-v2")
            || documents[p].Descendants().Where(static element => element.Name.LocalName == "IsTestProject").Any(x => x.Value == "true")).ToArray();
        if (changes is null)
        {
            return new ProjectSelection(projects, tests, ["Full validation requested or comparison history unavailable."]);
        }

        try
        {
            var graph = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            foreach (var (project, document) in documents)
            {
                if (document.Descendants().Where(static element => element.Name.LocalName == "Import").Any())
                {
                    throw new ArgumentException("Unevaluated import: " + project);
                }

                var dependencies = new HashSet<string>(StringComparer.Ordinal);
                foreach (var reference in document.Descendants().Where(static element => element.Name.LocalName == "ProjectReference"))
                {
                    var include = (string?)reference.Attribute("Include") ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(include) || include.IndexOfAny(['$', '*', ';']) >= 0)
                    {
                        throw new ArgumentException("Unevaluated project reference: " + project);
                    }

                    var dependency = Path.GetRelativePath(root, Path.GetFullPath(Path.Combine(root, Path.GetDirectoryName(project)!, include.Replace('\\', '/')))).Replace('\\', '/');
                    if (!documents.ContainsKey(dependency))
                    {
                        throw new ArgumentException("Unknown dependency: " + dependency);
                    }

                    dependencies.Add(dependency);
                }

                graph[project] = dependencies;
            }

            var edgePath = Path.Combine(root, "tools/SharedKernel.Repository.Tool/package-consumption-edges.json");
            if (File.Exists(edgePath))
            {
                foreach (var (consumer, values) in JsonNode.Parse(File.ReadAllText(edgePath))!.AsObject())
                {
                    if (!graph.TryGetValue(consumer, out var dependencies))
                    {
                        throw new ArgumentException("Unknown package consumer: " + consumer);
                    }

                    foreach (var value in values!.AsArray())
                    {
                        var dependency = value!.GetValue<string>();
                        if (!documents.ContainsKey(dependency))
                        {
                            throw new ArgumentException("Unknown package dependency: " + dependency);
                        }

                        dependencies.Add(dependency);
                    }
                }
            }

            var affected = new HashSet<string>(StringComparer.Ordinal);
            var reasons = new List<string>();
            foreach (var change in changes)
            {
                var path = change.Replace('\\', '/');
                var extension = Path.GetExtension(path);
                if (extension is ".props" or ".targets" or ".slnx" or ".globalconfig" || Path.GetFileName(path) is ".editorconfig" or ".gitattributes" or "global.json" or "NuGet.Config" or "NuGet.config"
                    || path.StartsWith(".github/", StringComparison.Ordinal) || path.StartsWith("tools/", StringComparison.Ordinal) || path.StartsWith("src/SharedKernel/SharedKernel.Style.", StringComparison.Ordinal)
                    || path.StartsWith("src/SharedKernel/SharedKernel.Testing.Analyzers/", StringComparison.Ordinal))
                {
                    throw new ArgumentException("Global or imported build input: " + path);
                }

                if (path.StartsWith("docs/", StringComparison.Ordinal) || path.StartsWith("provenance/", StringComparison.Ordinal) || path is "README.md" or "LICENSE.txt" or "AGENTS.md")
                {
                    continue;
                }

                var owner = projects.Where(p => path == p || path.StartsWith(Path.GetDirectoryName(p)!.Replace('\\', '/') + "/", StringComparison.Ordinal)).MaxBy(p => p.Length)
                    ?? throw new ArgumentException("Uncertain owner, including deleted projects: " + path);

                affected.Add(owner);
                reasons.Add(path + ": owning project " + owner);
            }

            bool added;
            do
            {
                added = false;
                foreach (var (project, references) in graph)
                {
                    if (!affected.Contains(project) && references.Overlaps(affected))
                    {
                        affected.Add(project);
                        reasons.Add(project + ": reverse transitive dependent");
                        added = true;
                    }
                }
            }
            while (added);
            return new ProjectSelection(affected.Order(StringComparer.Ordinal).ToArray(), tests.Where(affected.Contains).ToArray(), reasons.ToArray());
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or System.Text.Json.JsonException)
        {
            return new ProjectSelection(projects, tests, ["Full validation fallback: " + exception.Message]);
        }
    }
}
