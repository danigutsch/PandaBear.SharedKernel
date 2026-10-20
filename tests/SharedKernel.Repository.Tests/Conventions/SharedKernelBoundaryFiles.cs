using System.Xml.Linq;

namespace PandaBear.Repository.Tests.Conventions;

internal static class SharedKernelBoundaryFiles
{
    private static readonly string[] OptionalSubmoduleSegmentNames =
    [
        "Analyzers",
        "AspNet",
        "AspNetCore",
        "Azure",
        "CloudEvents",
        "CodeFixes",
        "Dapper",
        "EntityFrameworkCore",
        "Grafana",
        "Hosting",
        "Npgsql",
        "Redis",
        "SourceGenerator",
        "Web"
    ];

    private static readonly string[] ProductSpecificSharedKernelTestingTerms =
    [
        "\"bookings\"",
        "\"customers\"",
        "\"tours\"",
        "\"payments\"",
        "\"catalog\"",
        "\"public-web\""
    ];

    public static string[] FindSharedKernelRuntimeReferencesToSameFamilyOptionalSiblingSubmodules(string repositoryRoot)
    {
        return SharedKernelSourceProjectFiles(repositoryRoot)
            .SelectMany(filePath => FindRuntimeReferencesToSameFamilyOptionalSiblingSubmodules(repositoryRoot, filePath))
            .ToArray();
    }

    private static IEnumerable<string> FindRuntimeReferencesToSameFamilyOptionalSiblingSubmodules(
        string repositoryRoot,
        string filePath)
    {
        var referencingProjectName = Path.GetFileNameWithoutExtension(filePath);
        var relativePath = Path.GetRelativePath(repositoryRoot, filePath)
            .Replace(Path.DirectorySeparatorChar, '/');

        var projectReferences = RuntimeProjectReferences(filePath)
            .Where(reference => IsRuntimeProjectReference(reference.Element))
            .Where(reference => IsSameFamilyOptionalSiblingSubmoduleReference(
                referencingProjectName,
                reference.ReferencedProjectName))
            .Select(reference =>
                $"{relativePath}: {referencingProjectName} -> {reference.ReferencedProjectName}: "
                + $"ProjectReference Include=\"{reference.Include}\"");

        var packageReferences = PackageReferences(filePath)
            .Where(reference => IsSameFamilyOptionalSiblingSubmoduleReference(
                referencingProjectName,
                reference.PackageName))
            .Select(reference =>
                $"{relativePath}: {referencingProjectName} -> {reference.PackageName}: "
                + $"PackageReference Include=\"{reference.Include}\"");

        return projectReferences.Concat(packageReferences);
    }

    private static IEnumerable<(XElement Element, string Include, string PackageName)> PackageReferences(string filePath)
    {
        var document = XDocument.Load(filePath);

        foreach (var element in document.Descendants("PackageReference"))
        {
            var include = element.Attribute("Include")?.Value;
            if (string.IsNullOrWhiteSpace(include))
            {
                continue;
            }

            yield return (element, include, include);
        }
    }

    private static bool IsSameFamilyOptionalSiblingSubmoduleReference(
        string referencingProjectName,
        string referencedProjectName)
    {
        if (!IsSharedKernelProjectName(referencingProjectName)
            || !IsSharedKernelProjectName(referencedProjectName)
            || !IsOptionalSharedKernelSubmoduleProjectName(referencingProjectName)
            || !IsOptionalSharedKernelSubmoduleProjectName(referencedProjectName)
            || IsAbstractionsProjectName(referencedProjectName))
        {
            return false;
        }

        var referencingFamilyName = GetSharedKernelFamilyName(referencingProjectName);
        var referencedFamilyName = GetSharedKernelFamilyName(referencedProjectName);

        return referencingFamilyName.Equals(referencedFamilyName, StringComparison.OrdinalIgnoreCase)
            && !IsSameFamilyParentProjectReference(referencingFamilyName, referencingProjectName, referencedProjectName);
    }

    private static bool IsSameFamilyParentProjectReference(
        string familyName,
        string referencingProjectName,
        string referencedProjectName)
    {
        return referencedProjectName.Equals(familyName, StringComparison.OrdinalIgnoreCase)
            || (referencingProjectName.StartsWith($"{referencedProjectName}.", StringComparison.OrdinalIgnoreCase)
                && referencedProjectName.StartsWith($"{familyName}.", StringComparison.OrdinalIgnoreCase));
    }

    private static string GetSharedKernelFamilyName(string projectName)
    {
        var segments = projectName.Split('.');
        for (var index = 2; index < segments.Length; index++)
        {
            if (OptionalSubmoduleSegmentNames.Contains(segments[index], StringComparer.OrdinalIgnoreCase))
            {
                return string.Join('.', segments.Take(index));
            }
        }

        return projectName;
    }

    private static bool IsAbstractionsProjectName(string projectName) =>
        projectName.EndsWith(".Abstractions", StringComparison.OrdinalIgnoreCase);

    private static bool IsOptionalSharedKernelSubmoduleProjectName(string projectName)
    {
        return !GetSharedKernelFamilyName(projectName).Equals(projectName, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSharedKernelProjectName(string projectName)
    {
        return projectName.StartsWith("SharedKernel.", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRuntimeProjectReference(XElement element)
    {
        return !HasAttributeValue(element, "ReferenceOutputAssembly", "false")
            && !HasAnalyzerOrPackagingOutputItemType(element);
    }

    private static bool HasAnalyzerOrPackagingOutputItemType(XElement element)
    {
        var outputItemType = element.Attribute("OutputItemType")?.Value;

        return outputItemType is not null
            && (outputItemType.Contains("Analyzer", StringComparison.OrdinalIgnoreCase)
                || outputItemType.Contains("Pack", StringComparison.OrdinalIgnoreCase)
                || outputItemType.Contains("Package", StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasAttributeValue(XElement element, string attributeName, string value)
    {
        return element.Attribute(attributeName)?.Value.Equals(value, StringComparison.OrdinalIgnoreCase) == true;
    }

    private static IEnumerable<(XElement Element, string Include, string ReferencedProjectName)> RuntimeProjectReferences(
        string filePath)
    {
        var document = XDocument.Load(filePath);

        foreach (var element in document.Descendants("ProjectReference"))
        {
            var include = element.Attribute("Include")?.Value;
            if (string.IsNullOrWhiteSpace(include))
            {
                continue;
            }

            yield return (element, include, GetReferencedProjectName(filePath, include));
        }
    }

    private static string GetReferencedProjectName(string referencingProjectPath, string? include)
    {
        if (string.IsNullOrWhiteSpace(include))
        {
            throw new InvalidOperationException($"ProjectReference in {referencingProjectPath} is missing Include.");
        }

        var projectDirectory = Path.GetDirectoryName(referencingProjectPath) ?? throw new InvalidOperationException($"Project path has no directory: {referencingProjectPath}");
        var referencedProjectPath = Path.GetFullPath(Path.Combine(projectDirectory, include));

        return Path.GetFileNameWithoutExtension(referencedProjectPath);
    }

    private static IEnumerable<string> SharedKernelSourceProjectFiles(string repositoryRoot)
    {
        var sharedKernelRoot = Path.Combine(repositoryRoot, "src", "SharedKernel");

        return Directory.EnumerateFiles(sharedKernelRoot, "*.csproj", SearchOption.AllDirectories)
            .Where(IsSourceFile);
    }

    private static bool IsSourceFile(string filePath)
    {
        var normalizedPath = filePath.Replace(Path.DirectorySeparatorChar, '/');
        return !normalizedPath.Contains("/bin/", StringComparison.Ordinal)
            && !normalizedPath.Contains("/obj/", StringComparison.Ordinal);
    }

    public static string[] FindSharedKernelRuntimeReferencesToDescendantOptionalSubmodules(string repositoryRoot)
    {
        return SharedKernelSourceProjectFiles(repositoryRoot)
            .Where(IsPrimarySharedKernelProjectFile)
            .SelectMany(filePath => FindRuntimeReferencesToDescendantOptionalSubmodules(repositoryRoot, filePath))
            .ToArray();
    }

    private static IEnumerable<string> FindRuntimeReferencesToDescendantOptionalSubmodules(
        string repositoryRoot,
        string filePath)
    {
        var referencingProjectName = Path.GetFileNameWithoutExtension(filePath);
        var relativePath = Path.GetRelativePath(repositoryRoot, filePath)
            .Replace(Path.DirectorySeparatorChar, '/');

        var projectReferences = RuntimeProjectReferences(filePath)
            .Where(reference => IsRuntimeProjectReference(reference.Element))
            .Where(reference => IsDescendantSharedKernelProjectReference(
                referencingProjectName,
                reference.ReferencedProjectName))
            .Where(reference => !IsAbstractionsProjectName(reference.ReferencedProjectName))
            .Select(reference =>
                $"{relativePath}: {referencingProjectName} -> {reference.ReferencedProjectName}: "
                + $"ProjectReference Include=\"{reference.Include}\"");

        var packageReferences = PackageReferences(filePath)
            .Where(reference => IsDescendantSharedKernelProjectReference(
                referencingProjectName,
                reference.PackageName))
            .Where(reference => !IsAbstractionsProjectName(reference.PackageName))
            .Select(reference =>
                $"{relativePath}: {referencingProjectName} -> {reference.PackageName}: "
                + $"PackageReference Include=\"{reference.Include}\"");

        return projectReferences.Concat(packageReferences);
    }

    private static bool IsDescendantSharedKernelProjectReference(
        string referencingProjectName,
        string referencedProjectName)
    {
        return referencedProjectName.StartsWith("SharedKernel.", StringComparison.OrdinalIgnoreCase)
            && referencedProjectName.StartsWith($"{referencingProjectName}.", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPrimarySharedKernelProjectFile(string filePath)
    {
        var projectName = Path.GetFileNameWithoutExtension(filePath);

        return IsPrimarySharedKernelProjectName(projectName);
    }

    private static bool IsPrimarySharedKernelProjectName(string projectName)
    {
        return projectName.StartsWith("SharedKernel.", StringComparison.OrdinalIgnoreCase)
            && !IsAbstractionsProjectName(projectName)
            && !IsSharedKernelTestingSubmodule(projectName)
            && !projectName.Split('.')
                .Skip(2)
                .Any(segment => OptionalSubmoduleSegmentNames.Contains(segment, StringComparer.OrdinalIgnoreCase));
    }

    private static bool IsSharedKernelTestingSubmodule(string projectName)
    {
        return projectName.StartsWith("SharedKernel.Testing.", StringComparison.OrdinalIgnoreCase);
    }

    public static string[] FindSharedKernelProductReferences(string repositoryRoot)
    {
        return SharedKernelSourceFiles(repositoryRoot)
            .Where(filePath => filePath.EndsWith(".csproj", StringComparison.Ordinal))
            .SelectMany(filePath => XDocument.Load(filePath).Descendants("ProjectReference")
                .Select(reference => reference.Attribute("Include")?.Value
                    ?? throw new InvalidOperationException($"ProjectReference in {filePath} is missing Include."))
                .Where(include => !IsSharedKernelProjectName(Path.GetFileNameWithoutExtension(include.Replace('\\', '/'))))
                .Select(include => $"{Path.GetRelativePath(repositoryRoot, filePath).Replace('\\', '/')}: ProjectReference Include=\"{include}\""))
            .ToArray();
    }

    private static IEnumerable<string> SharedKernelSourceFiles(string repositoryRoot)
    {
        var sourceRoot = Path.Combine(repositoryRoot, "src", "SharedKernel");
        var testsRoot = Path.Combine(repositoryRoot, "tests");

        return Directory.EnumerateFiles(sourceRoot, "*.csproj", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories))
            .Concat(Directory.EnumerateDirectories(testsRoot, "SharedKernel*", SearchOption.TopDirectoryOnly)
                .SelectMany(directory => Directory.EnumerateFiles(directory, "*.csproj", SearchOption.AllDirectories)
                    .Concat(Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))))
            .Where(IsSourceFile);
    }

    public static string[] FindSharedKernelCoreSegmentProjectViolations(string repositoryRoot)
    {
        return SharedKernelSourceProjectFiles(repositoryRoot)
            .Where(HasCoreProjectNameSegment)
            .Select(filePath =>
                $"{Path.GetRelativePath(repositoryRoot, filePath).Replace(Path.DirectorySeparatorChar, '/')}: "
                + $"{Path.GetFileNameWithoutExtension(filePath)} uses Core as a project-name segment")
            .ToArray();
    }

    private static bool HasCoreProjectNameSegment(string filePath)
    {
        var projectName = Path.GetFileNameWithoutExtension(filePath);

        return projectName.Split('.')
            .Any(segment => segment.Equals("Core", StringComparison.OrdinalIgnoreCase));
    }

    public static string[] FindSharedKernelEntityFrameworkCoreAdapterNamingViolations(string repositoryRoot)
    {
        return SharedKernelSourceProjectFiles(repositoryRoot)
            .Where(ReferencesEntityFrameworkCorePackage)
            .Where(filePath => !NamesEntityFrameworkCoreAdapter(filePath))
            .Select(filePath => Path.GetRelativePath(repositoryRoot, filePath).Replace(Path.DirectorySeparatorChar, '/'))
            .ToArray();
    }

    private static bool NamesEntityFrameworkCoreAdapter(string filePath)
    {
        var projectName = Path.GetFileNameWithoutExtension(filePath);

        return projectName.EndsWith(".EntityFrameworkCore", StringComparison.OrdinalIgnoreCase)
            || projectName.Contains(".EntityFrameworkCore.", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ReferencesEntityFrameworkCorePackage(string filePath)
    {
        var document = XDocument.Load(filePath);

        return document.Descendants("PackageReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Any(packageName => packageName is not null && packageName.Contains("EntityFrameworkCore", StringComparison.OrdinalIgnoreCase));
    }

    public static string[] FindProductSpecificSharedKernelTestingCoupling(string filePath)
    {
        var repositoryRoot = GetRepositoryRoot();
        var lines = File.ReadAllLines(filePath);
        var offenses = new List<string>();

        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            var line = lines[lineIndex];
            if (!ProductSpecificSharedKernelTestingTerms.Any(term => line.Contains(term, StringComparison.Ordinal)))
            {
                continue;
            }

            offenses.Add($"{Path.GetRelativePath(repositoryRoot, filePath).Replace('\\', '/')}:L{lineIndex + 1} {line.Trim()}");
        }

        return [.. offenses];
    }
    public static string GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SharedKernel.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not locate repository root.");
    }

    public static bool IsGeneratedTestPath(string path)
    {
        var normalizedPath = path.Replace('\\', '/');
        return normalizedPath.Contains("/bin/", StringComparison.Ordinal)
            || normalizedPath.Contains("/obj/", StringComparison.Ordinal)
            || normalizedPath.Contains("/Snapshots/", StringComparison.Ordinal)
            || normalizedPath.EndsWith(".feature.cs", StringComparison.Ordinal);
    }

    public static string[] FindProviderNeutralSharedKernelAdapterPackageReferences(string repositoryRoot)
    {
        var sharedKernelRoot = Path.Combine(repositoryRoot, "src", "SharedKernel");

        return Directory.EnumerateFiles(sharedKernelRoot, "*.csproj", SearchOption.AllDirectories)
            .Where(IsProviderNeutralSharedKernelProject)
            .SelectMany(filePath => FindAdapterPackageReferences(repositoryRoot, filePath))
            .ToArray();
    }

    private static IEnumerable<string> FindAdapterPackageReferences(string repositoryRoot, string filePath)
    {
        var relativePath = Path.GetRelativePath(repositoryRoot, filePath)
            .Replace(Path.DirectorySeparatorChar, '/');

        var document = XDocument.Load(filePath);

        return document.Descendants("PackageReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(packageName => packageName is not null && IsAdapterPackage(packageName))
            .Select(packageName => $"{relativePath}: PackageReference Include=\"{packageName}\"");
    }

    private static bool IsAdapterPackage(string packageName)
    {
        return packageName.Equals("Npgsql", StringComparison.OrdinalIgnoreCase)
            || packageName.StartsWith("Npgsql.", StringComparison.OrdinalIgnoreCase)
            || packageName.Contains("EntityFrameworkCore", StringComparison.OrdinalIgnoreCase)
            || packageName.Equals("Dapper", StringComparison.OrdinalIgnoreCase)
            || packageName.StartsWith("Azure.", StringComparison.OrdinalIgnoreCase)
            || packageName.StartsWith("Aspire.Npgsql", StringComparison.OrdinalIgnoreCase)
            || packageName.StartsWith("Aspire.StackExchange.Redis", StringComparison.OrdinalIgnoreCase)
            || packageName.StartsWith("StackExchange.Redis", StringComparison.OrdinalIgnoreCase)
            || packageName.StartsWith("RabbitMQ.", StringComparison.OrdinalIgnoreCase)
            || packageName.StartsWith("MassTransit", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsProviderNeutralSharedKernelProject(string filePath)
    {
        var projectName = Path.GetFileNameWithoutExtension(filePath);

        return projectName.StartsWith("SharedKernel.", StringComparison.OrdinalIgnoreCase)
            && !projectName.Contains(".Npgsql", StringComparison.OrdinalIgnoreCase)
            && !projectName.Contains(".EntityFrameworkCore", StringComparison.OrdinalIgnoreCase)
            && !projectName.Contains(".Dapper", StringComparison.OrdinalIgnoreCase)
            && !projectName.Contains(".Azure", StringComparison.OrdinalIgnoreCase)
            && !projectName.Contains(".Redis", StringComparison.OrdinalIgnoreCase)
            && !projectName.Contains(".CloudEvents", StringComparison.OrdinalIgnoreCase)
            && !projectName.Contains(".Aspire.Hosting", StringComparison.OrdinalIgnoreCase);
    }
}
