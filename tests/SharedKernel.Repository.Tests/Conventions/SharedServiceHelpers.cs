using System.Xml.Linq;

namespace SharedKernel.Repository.Tests.Conventions;

/// <summary>
/// Finds the helpers through which test projects borrow the services of slices: the solution projects, neither test projects nor slice
/// services, whose sources contain a variable that a slice service hands over as a string literal.
/// </summary>
internal static class SharedServiceHelpers
{
    /// <summary>Maps each variable that a slice service hands over to the helper projects that read it.</summary>
    /// <param name="root">The repository root.</param>
    /// <param name="configuration">The CI configuration.</param>
    /// <param name="graph">The solution graph.</param>
    /// <returns>The helpers of each variable that has one.</returns>
    internal static Dictionary<string, string[]> Find(string root, CiConfiguration configuration, SolutionGraph graph)
    {
        var services = configuration.Slices.Select(static slice => slice.Service?.Project).OfType<string>().ToHashSet(StringComparer.Ordinal);
        var variables = configuration.Slices.SelectMany(static slice => slice.Service?.ValueVariables ?? []).ToArray();
        var sources = graph.Projects
            .Where(project => !graph.Tests.Contains(project) && !services.Contains(project))
            .Select(project => (Project: project, Text: Sources(root, project)))
            .ToArray();
        return variables
            .Select(variable => (Variable: variable, Helpers: sources.Where(source => source.Text.Contains('"' + variable + '"', StringComparison.Ordinal)).Select(static source => source.Project).ToArray()))
            .Where(static entry => entry.Helpers.Length > 0)
            .ToDictionary(static entry => entry.Variable, static entry => entry.Helpers, StringComparer.Ordinal);
    }

    /// <summary>Returns the variables that <paramref name="project"/> borrows: those whose helpers it references directly.</summary>
    /// <param name="root">The repository root.</param>
    /// <param name="project">The repository-relative test project.</param>
    /// <param name="helpers">The helpers of each variable.</param>
    /// <returns>The variables, in the order of <paramref name="helpers"/>.</returns>
    internal static string[] Borrowed(string root, string project, Dictionary<string, string[]> helpers)
    {
        var directory = Path.GetDirectoryName(Path.Combine(root, project))!;
        var references = XDocument.Load(Path.Combine(root, project)).Descendants("ProjectReference")
            .Select(reference => reference.Attribute("Include")?.Value)
            .OfType<string>()
            .Select(include => Path.GetRelativePath(root, Path.GetFullPath(Path.Combine(directory, include.Replace('\\', '/')))).Replace('\\', '/'))
            .ToHashSet(StringComparer.Ordinal);
        return [.. helpers.Where(entry => entry.Value.Any(references.Contains)).Select(static entry => entry.Key)];
    }

    private static string Sources(string root, string project)
    {
        var directory = Path.GetDirectoryName(Path.Combine(root, project))!;
        var separator = Path.DirectorySeparatorChar;
        return string.Concat(Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains(separator + "bin" + separator, StringComparison.Ordinal) && !file.Contains(separator + "obj" + separator, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .Select(File.ReadAllText));
    }
}
