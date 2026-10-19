using System.Xml.Linq;

namespace SharedKernel.Repository.Tests.Conventions;

internal static class SharedKernelPackagingConventionTestFiles
{
    public static string GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "SharedKernel.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }

    public static IEnumerable<string> GetPackageMetadataViolations(string projectPath)
    {
        var document = XDocument.Load(projectPath);
        var projectDirectory = Path.GetDirectoryName(projectPath) ?? throw new InvalidOperationException("Project path has no directory.");
        var packageId = GetProperty(document, "PackageId");
        if (packageId != "PandaBear." + Path.GetFileNameWithoutExtension(projectPath))
        {
            yield return $"{projectPath}: PackageId must match the project file name.";
        }

        if (string.IsNullOrWhiteSpace(GetProperty(document, "PackageTags")))
        {
            yield return $"{projectPath}: PackageTags is required.";
        }

        if (File.Exists(Path.Combine(projectDirectory, "README.md")))
        {
            if (GetProperty(document, "PackageReadmeFile") != "README.md")
            {
                yield return $"{projectPath}: PackageReadmeFile must include README.md.";
            }

            if (!PacksReadme(document))
            {
                yield return $"{projectPath}: README.md must be packed.";
            }
        }

        var targetFramework = GetProperty(document, "TargetFramework");
        if (string.Equals(targetFramework, "netstandard2.0", StringComparison.OrdinalIgnoreCase)
            && IsRoslynPackage(packageId)
            && !string.Equals(GetProperty(document, "IsAotCompatible"), "false", StringComparison.OrdinalIgnoreCase))
        {
            yield return $"{projectPath}: netstandard2.0 Roslyn packages must opt out of AOT compatibility.";
        }
    }

    public static string GetProperty(string projectPath, string propertyName)
    {
        var document = XDocument.Load(projectPath);
        return GetProperty(document, propertyName);
    }

    public static IEnumerable<string> GetAnalyzerPackageLayoutViolations(
        string projectPath,
        params string[] expectedAnalyzerDllNames)
    {
        var document = XDocument.Load(projectPath);
        if (!string.Equals(GetProperty(document, "IncludeBuildOutput"), "false", StringComparison.OrdinalIgnoreCase))
        {
            yield return $"{projectPath}: analyzer packages must not include build output under lib by default.";
        }

        if (!HasPackItem(document, "README.md", "\\"))
        {
            yield return $"{projectPath}: README.md must be packed at package root.";
        }

        if (!HasPackItem(document, "_._", "lib/netstandard2.0/"))
        {
            yield return $"{projectPath}: analyzer packages must include lib/netstandard2.0/_._ placeholder.";
        }

        foreach (var expectedAnalyzerDllName in expectedAnalyzerDllNames)
        {
            if (!PacksAnalyzerDll(document, expectedAnalyzerDllName))
            {
                yield return $"{projectPath}: {expectedAnalyzerDllName} must be packed under analyzers/dotnet/cs.";
            }
        }

        foreach (var libDll in GetPackedLibDlls(document))
        {
            yield return $"{projectPath}: {libDll} must not be packed under lib.";
        }
    }

    public static HashSet<string> GetYamlPathEntries(string content)
    {
        var entries = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in content.Split('\n'))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("- ", StringComparison.Ordinal))
            {
                continue;
            }

            entries.Add(trimmed[2..].Trim());
        }

        return entries;
    }

    private static string GetProperty(XDocument document, string propertyName)
    {
        return document.Descendants()
            .FirstOrDefault(element => string.Equals(element.Name.LocalName, propertyName, StringComparison.Ordinal))
            ?.Value.Trim() ?? string.Empty;
    }

    private static bool PacksReadme(XDocument document)
    {
        return document.Descendants().Any(element =>
            element.Name.LocalName == "None"
            && element.Attribute("Include")?.Value == "README.md"
            && string.Equals(element.Attribute("Pack")?.Value, "true", StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasPackItem(XDocument document, string include, string packagePath)
    {
        return document.Descendants().Any(element =>
            element.Name.LocalName == "None"
            && string.Equals(element.Attribute("Include")?.Value, include, StringComparison.Ordinal)
            && string.Equals(element.Attribute("Pack")?.Value, "true", StringComparison.OrdinalIgnoreCase)
            && string.Equals(element.Attribute("PackagePath")?.Value, packagePath, StringComparison.Ordinal));
    }

    private static bool PacksAnalyzerDll(XDocument document, string expectedAnalyzerDllName)
    {
        var selfDllName = GetProperty(document, "AssemblyName") is { Length: > 0 } assemblyName
            ? assemblyName + ".dll"
            : GetProperty(document, "RootNamespace") + ".dll";

        return document.Descendants().Any(element =>
            element.Name.LocalName == "None"
            && string.Equals(element.Attribute("Pack")?.Value, "true", StringComparison.OrdinalIgnoreCase)
            && string.Equals(element.Attribute("PackagePath")?.Value, "analyzers/dotnet/cs", StringComparison.Ordinal)
            && (string.Equals(element.Attribute("Link")?.Value, expectedAnalyzerDllName, StringComparison.Ordinal)
                || (element.Attribute("Include")?.Value.EndsWith(expectedAnalyzerDllName, StringComparison.Ordinal) ?? false)
                || (string.Equals(expectedAnalyzerDllName, selfDllName, StringComparison.Ordinal)
                    && string.Equals(element.Attribute("Include")?.Value, "$(OutputPath)$(AssemblyName).dll", StringComparison.Ordinal))));
    }

    private static IEnumerable<string> GetPackedLibDlls(XDocument document)
    {
        return document.Descendants()
            .Where(static element => element.Name.LocalName == "None")
            .Where(static element => string.Equals(element.Attribute("Pack")?.Value, "true", StringComparison.OrdinalIgnoreCase))
            .Where(static element => element.Attribute("Include")?.Value.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ?? false)
            .Where(static element => element.Attribute("PackagePath")?.Value.StartsWith("lib/", StringComparison.OrdinalIgnoreCase) ?? false)
            .Select(static element => element.Attribute("Include")?.Value ?? string.Empty);
    }

    private static bool IsRoslynPackage(string packageId) =>
        packageId.EndsWith(".Analyzers", StringComparison.Ordinal)
        || packageId.EndsWith(".CodeFixes", StringComparison.Ordinal)
        || packageId.EndsWith(".SourceGenerator", StringComparison.Ordinal);
}
