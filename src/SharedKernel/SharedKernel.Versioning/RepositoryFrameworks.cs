using System.Xml.Linq;

namespace SharedKernel.Versioning;

/// <summary>Reads the target frameworks a repository builds for from its root <c>Directory.Build.props</c>.</summary>
public static class RepositoryFrameworks
{
    private const string FileName = "Directory.Build.props";

    /// <summary>
    /// Reads the frameworks from <c>&lt;repoRoot&gt;/Directory.Build.props</c>: the <c>TargetFrameworks</c> property split on <c>;</c>, or
    /// else the <c>TargetFramework</c> property, with <c>$(Name)</c> references resolved from properties of the same file.
    /// </summary>
    /// <param name="repoRoot">The repository root.</param>
    /// <returns>The frameworks, each a simple moniker such as <c>net11.0</c>, in file order.</returns>
    /// <exception cref="ArgumentException"><paramref name="repoRoot"/> is <see langword="null"/>, empty, or white-space.</exception>
    /// <exception cref="InvalidOperationException">
    /// The file is missing, the framework property is missing or conditional, or a value is not a moniker; the message names the file.
    /// </exception>
    public static IReadOnlyList<string> Read(string repoRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repoRoot);
        var path = Path.Combine(repoRoot, FileName);
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"Cannot read the repository's target framework: '{path}' does not exist.");
        }

        var properties = XDocument.Load(path).Root!.Elements("PropertyGroup").SelectMany(static group => group.Elements()
            .Select(property => (property.Name.LocalName, property.Value, Conditional: group.Attribute("Condition") is not null || property.Attribute("Condition") is not null)))
            .ToList();
        foreach (var name in new[] { "TargetFrameworks", "TargetFramework" })
        {
            var matches = properties.Where(property => property.LocalName == name).ToList();
            if (matches.Count == 0)
            {
                continue;
            }

            if (matches.Exists(static property => property.Conditional))
            {
                throw new InvalidOperationException($"The property '{name}' in '{path}' is conditional; the repository's target framework must be set unconditionally.");
            }

            // MSBuild builds TargetFramework when TargetFrameworks is empty, as a project that clears the list does.
            if (name == "TargetFrameworks" && matches[^1].Value.Trim().Length == 0)
            {
                continue;
            }

            return Resolve(matches[^1].Value, name, properties, path, [name]);
        }

        throw new InvalidOperationException($"'{path}' sets neither 'TargetFrameworks' nor 'TargetFramework'.");
    }

    private static List<string> Resolve(
        string value, string name, List<(string LocalName, string Value, bool Conditional)> properties, string path, HashSet<string> resolving)
    {
        var frameworks = new List<string>();
        foreach (var part in value.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var framework = part;
            if (framework.StartsWith("$(", StringComparison.Ordinal) && framework.EndsWith(')', StringComparison.Ordinal))
            {
                var referenced = framework[2..^1];
                if (!resolving.Add(referenced))
                {
                    throw new InvalidOperationException($"The property '{name}' in '{path}' references '{framework}', which refers to itself.");
                }

                var matches = properties.Where(property => property.LocalName == referenced).ToList();
                if (matches.Count == 0 || matches.Exists(static property => property.Conditional))
                {
                    throw new InvalidOperationException($"The property '{name}' in '{path}' references '{framework}', which is {(matches.Count == 0 ? "not defined" : "conditional")} in that file.");
                }

                frameworks.AddRange(Resolve(matches[^1].Value, referenced, properties, path, resolving));
                resolving.Remove(referenced);
                continue;
            }

            if (!ConsumerFrameworks.IsValid(framework))
            {
                throw new InvalidOperationException($"The property '{name}' in '{path}' has '{framework}', which is not a target framework such as net11.0.");
            }

            frameworks.Add(framework);
        }

        return frameworks.Count == 0
            ? throw new InvalidOperationException($"The property '{name}' in '{path}' is empty.")
            : frameworks;
    }
}
