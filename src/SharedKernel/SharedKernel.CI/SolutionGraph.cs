using System.Xml;
using System.Xml.Linq;
using SharedKernel.IO;

namespace SharedKernel.CI;

/// <summary>
/// The projects of a solution and the edges that selection reads from their project files without evaluating MSBuild:
/// <c>ProjectReference</c> items, and <c>Compile</c> and data items (<c>None</c>, <c>Content</c>, <c>EmbeddedResource</c>,
/// <c>AdditionalFiles</c>) with an explicit path. Paths are repository-relative with <c>/</c> separators.
/// </summary>
public sealed class SolutionGraph
{
    private static readonly HashSet<string> DataItemTypes = new(["None", "Content", "EmbeddedResource", "AdditionalFiles"], StringComparer.Ordinal);

    private readonly Dictionary<string, string[]> references;
    private readonly Dictionary<string, List<string>> compileInputs;

    private SolutionGraph(
        string solution,
        string[] projects,
        HashSet<string> tests,
        Dictionary<string, string[]> references,
        Dictionary<string, List<string>> compileInputs,
        HashSet<string> unread,
        string? uncertainty)
    {
        Unread = unread;
        Solution = solution;
        Projects = projects;
        Tests = tests;
        this.references = references;
        this.compileInputs = compileInputs;
        Uncertainty = uncertainty;
    }

    /// <summary>Gets the repository-relative solution path.</summary>
    public string Solution { get; }

    /// <summary>Gets every solution project, in ordinal order.</summary>
    public IReadOnlyList<string> Projects { get; }

    /// <summary>Gets the projects that reference <c>xunit.v3.mtp-v2</c> or set <c>IsTestProject</c> to <c>true</c>.</summary>
    public IReadOnlySet<string> Tests { get; }

    /// <summary>Gets the solution projects whose project file cannot be read.</summary>
    internal HashSet<string> Unread { get; }

    /// <summary>Gets why the edges are incomplete, or <see langword="null"/> when every project file was read and evaluated.</summary>
    internal string? Uncertainty { get; }

    /// <summary>Loads the solution and its project files under <paramref name="root"/>.</summary>
    /// <exception cref="InvalidOperationException">The solution cannot be read, or names a project outside the root.</exception>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static SolutionGraph Load(string root, string solution)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(solution);
        XDocument document;
        try
        {
            document = XDocument.Load(RepositoryPath.Parse(solution).ResolveUnder(root));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or XmlException or InvalidOperationException)
        {
            throw new InvalidOperationException(CiConfigurationFile.RelativePath + ": $.solution names " + solution + ", which cannot be read.", exception);
        }

        var solutionDirectory = Path.GetDirectoryName(solution.Replace('/', Path.DirectorySeparatorChar)) ?? string.Empty;
        var projects = document.Descendants("Project")
            .Select(project => ToRepositoryPath(root, Path.Combine(root, solutionDirectory, ((string?)project.Attribute("Path") ?? string.Empty).Replace('\\', '/')))
                ?? throw new InvalidOperationException(solution + " names a project outside the repository."))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var known = projects.ToHashSet(StringComparer.Ordinal);
        var tests = new HashSet<string>(StringComparer.Ordinal);
        var references = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var compileInputs = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var unread = new HashSet<string>(StringComparer.Ordinal);
        string? uncertainty = null;
        foreach (var project in projects)
        {
            XDocument projectDocument;
            try
            {
                projectDocument = XDocument.Load(RepositoryPath.Parse(project).ResolveUnder(root));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or XmlException or InvalidOperationException)
            {
                uncertainty ??= project + " cannot be read.";
                unread.Add(project);
                references[project] = [];
                continue;
            }

            var elements = projectDocument.Descendants().ToArray();
            if (elements.Any(static element => element.Name.LocalName == "PackageReference" && (string?)element.Attribute("Include") == "xunit.v3.mtp-v2")
                || elements.Any(static element => element.Name.LocalName == "IsTestProject" && element.Value.Trim() == "true"))
            {
                tests.Add(project);
            }

            if (elements.FirstOrDefault(static element => element.Name.LocalName == "Import" && !IsSdkImport(element)) is not null)
            {
                uncertainty ??= project + " imports a file whose items selection does not evaluate.";
            }

            var projectDirectory = Path.GetDirectoryName(Path.Combine(root, project))!;
            var dependencies = new List<string>();
            foreach (var reference in elements.Where(static element => element.Name.LocalName == "ProjectReference"))
            {
                var dependency = Resolve(root, projectDirectory, (string?)reference.Attribute("Include"));
                if (dependency is null || !known.Contains(dependency))
                {
                    uncertainty ??= project + " has a project reference that selection cannot resolve to a solution project.";
                    continue;
                }

                dependencies.Add(dependency);
            }

            references[project] = [.. dependencies];
            foreach (var input in elements.Where(static element => element.Name.LocalName == "Compile" && element.Attribute("Include") is not null))
            {
                var path = Resolve(root, projectDirectory, (string?)input.Attribute("Include"));
                if (path is null)
                {
                    uncertainty ??= project + " has a compile item that selection cannot resolve to one file.";
                    continue;
                }

                AddInput(compileInputs, path, project);
            }

            foreach (var input in elements.Where(static element => DataItemTypes.Contains(element.Name.LocalName) && element.Attribute("Include") is not null))
            {
                var include = (string)input.Attribute("Include")!;
                if (include.IndexOfAny(['$', '@', '%']) >= 0)
                {
                    continue;
                }

                if (include.IndexOfAny(['*', '?', ';']) < 0)
                {
                    if (Resolve(root, projectDirectory, include) is { } path)
                    {
                        AddInput(compileInputs, path, project);
                    }

                    continue;
                }

                if (include.Split(';').Any(part => !StaysInside(projectDirectory, part)))
                {
                    uncertainty ??= project + " has a data item pattern that selection cannot resolve outside its project directory.";
                }
            }
        }

        return new SolutionGraph(solution, projects, tests, references, compileInputs, unread, uncertainty);
    }

    /// <summary>
    /// Returns the projects that own <paramref name="path"/>: the projects that compile or include it as a data item by an
    /// explicit path and the project whose directory most closely contains it.
    /// </summary>
    internal string[] Owners(string path)
    {
        var owners = compileInputs.TryGetValue(path, out var inputs) ? new HashSet<string>(inputs, StringComparer.Ordinal) : new HashSet<string>(StringComparer.Ordinal);
        var directoryOwner = Projects
            .Where(project => path == project || (project.Contains('/', StringComparison.Ordinal)
                && path.StartsWith(project[..(project.LastIndexOf('/', StringComparison.Ordinal) + 1)], StringComparison.Ordinal)))
            .MaxBy(static project => project.LastIndexOf('/', StringComparison.Ordinal));
        if (directoryOwner is not null)
        {
            owners.Add(directoryOwner);
        }

        return [.. owners.Order(StringComparer.Ordinal)];
    }

    /// <summary>Returns <paramref name="projects"/> and every project that references one of them, directly or transitively.</summary>
    internal HashSet<string> WithDependents(IEnumerable<string> projects)
    {
        var affected = new HashSet<string>(projects, StringComparer.Ordinal);
        bool added;
        do
        {
            added = false;
            foreach (var (project, dependencies) in references)
            {
                if (!affected.Contains(project) && dependencies.Any(affected.Contains))
                {
                    added = affected.Add(project);
                }
            }
        }
        while (added);
        return affected;
    }

    /// <summary>Returns <paramref name="project"/> and every project it references, directly or transitively.</summary>
    public IReadOnlySet<string> Closure(string project)
    {
        var closure = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<string>([project]);
        while (pending.TryPop(out var current))
        {
            if (closure.Add(current) && references.TryGetValue(current, out var dependencies))
            {
                foreach (var dependency in dependencies)
                {
                    pending.Push(dependency);
                }
            }
        }

        return closure;
    }

    private static void AddInput(Dictionary<string, List<string>> inputs, string path, string project)
    {
        if (!inputs.TryGetValue(path, out var owners))
        {
            inputs[path] = owners = [];
        }

        owners.Add(project);
    }

    private static bool StaysInside(string projectDirectory, string pattern)
    {
        var literal = pattern.Replace('\\', '/');
        var wildcard = literal.IndexOfAny(['*', '?']);
        var directory = wildcard < 0 ? Path.GetDirectoryName(literal) ?? string.Empty : literal[..(literal[..wildcard].LastIndexOf('/', StringComparison.Ordinal) + 1)];
        var full = Path.GetFullPath(Path.Combine(projectDirectory, directory));
        var inside = Path.GetFullPath(projectDirectory);
        return full == inside || full.StartsWith(inside + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }

    private static bool IsSdkImport(XElement import) =>
        (string?)import.Attribute("Sdk") == "Microsoft.NET.Sdk" && (string?)import.Attribute("Project") is "Sdk.props" or "Sdk.targets";

    // An include with a property, a wildcard, or a list needs MSBuild evaluation, so it resolves to nothing.
    private static string? Resolve(string root, string projectDirectory, string? include) =>
        string.IsNullOrWhiteSpace(include) || include.IndexOfAny(['$', '@', '%', '*', '?', ';']) >= 0
            ? null
            : ToRepositoryPath(root, Path.Combine(projectDirectory, include.Replace('\\', '/')));

    private static string? ToRepositoryPath(string root, string path)
    {
        var relative = Path.GetRelativePath(root, Path.GetFullPath(path)).Replace('\\', '/');
        return relative == "." || relative.StartsWith("../", StringComparison.Ordinal) || relative == ".." || Path.IsPathRooted(relative) ? null : relative;
    }
}
