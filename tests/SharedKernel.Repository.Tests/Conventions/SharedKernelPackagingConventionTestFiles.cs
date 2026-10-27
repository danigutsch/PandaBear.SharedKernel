using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace PandaBear.Repository.Tests.Conventions;

/// <summary>Reads project files and YAML lists for the packaging convention tests.</summary>
internal static partial class SharedKernelPackagingConventionTestFiles
{
    /// <summary>Walks up from the test binary directory to the first directory that contains <c>SharedKernel.slnx</c> and returns it.</summary>
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

    /// <summary>Returns a message for each <c>HintName</c> assignment or <c>AddSource</c> call in <paramref name="text"/> whose string literal does not end in <c>.g.cs</c>.</summary>
    public static IEnumerable<string> GetHintNameLiteralViolations(string path, string text) =>
        HintNameLiteral().Matches(text)
            .Select(static match => match.Groups["name"].Value)
            .Where(static name => !name.EndsWith(".g.cs", StringComparison.Ordinal))
            .Select(name => $"{path}: hint name \"{name}\" must end in .g.cs.");

    [GeneratedRegex(@"(?:HintName\w*\s*=|AddSource\(\s*)\s*""(?<name>[^""]*)""")]
    private static partial Regex HintNameLiteral();

    /// <summary>Returns a message for each missing package metadata item of a packable project: a package ID that differs from the project name, missing tags, a missing or unpacked README, and a netstandard2.0 Roslyn package that does not opt out of AOT compatibility.</summary>
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

        if (!File.Exists(Path.Combine(projectDirectory, "README.md")))
        {
            yield return $"{projectPath}: README.md is required next to every packable project.";
        }
        else
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

    /// <summary>Loads the project file at <paramref name="projectPath"/> and returns the trimmed text of its first <paramref name="propertyName"/> element, or an empty string when none exists.</summary>
    public static string GetProperty(string projectPath, string propertyName)
    {
        var document = XDocument.Load(projectPath);
        return GetProperty(document, propertyName);
    }

    /// <summary>Returns a message for each way an analyzer project breaks the analyzer-only package layout: build output under lib, an unpacked README or <c>_._</c> placeholder, a missing analyzer DLL under analyzers/dotnet/cs, or a DLL packed under lib.</summary>
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
            if (!PacksAnalyzerDll(document, projectPath, expectedAnalyzerDllName))
            {
                yield return $"{projectPath}: {expectedAnalyzerDllName} must be packed under analyzers/dotnet/cs.";
            }
        }

        foreach (var libDll in GetPackedLibDlls(document))
        {
            yield return $"{projectPath}: {libDll} must not be packed under lib.";
        }
    }

    /// <summary>Returns the trimmed text after the dash of every list item line in <paramref name="content"/>, splitting on line feeds.</summary>
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

    /// <summary>Returns the trimmed text of the first element of <paramref name="document"/> named <paramref name="propertyName"/>, or an empty string when none exists.</summary>
    public static string GetProperty(XDocument document, string propertyName)
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

    private static bool PacksAnalyzerDll(XDocument document, string projectPath, string expectedAnalyzerDllName)
    {
        var selfDllName = GetProperty(document, "AssemblyName") is { Length: > 0 } assemblyName
            ? assemblyName + ".dll"
            : Path.GetFileNameWithoutExtension(projectPath) + ".dll";

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
