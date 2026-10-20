using System.Text.Json;

namespace SharedKernel.Documentation;

/// <summary>
/// Checks machine-readable documentation facts against configured source files.
/// </summary>
public static class DocumentationConformanceChecker
{
    /// <summary>
    /// Checks documentation facts from a JSON configuration file.
    /// </summary>
    /// <param name="rootPath">The repository root path.</param>
    /// <param name="configPath">The configuration path relative to the repository root.</param>
    /// <returns>The number of conformance checks completed.</returns>
    public static int Check(string rootPath, string configPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(configPath);

        var fullRootPath = Path.GetFullPath(rootPath);
        var fullConfigPath = ResolveRepositoryPath(fullRootPath, configPath, "Conformance config");
        var config = JsonSerializer.Deserialize(
            File.ReadAllText(fullConfigPath),
            DocumentationConformanceJsonContext.Default.DocumentationConformanceConfig)
            ?? throw new InvalidOperationException($"Could not read documentation conformance config: {configPath}");
        ValidateConfig(config, configPath);

        foreach (var check in config.Checks)
        {
            CheckFact(fullRootPath, check);
        }

        return config.Checks.Count;
    }

    private static void CheckFact(string rootPath, DocumentationFactCheck check)
    {
        var fullDocumentPath = ResolveRepositoryPath(rootPath, check.DocumentPath, "Documentation fact document");
        var documentedIdentifiers = DocumentationSourceFacts.ReadMarkedFactIdentifiers(
            fullDocumentPath,
            check.DocumentPath,
            check.MarkerName,
            check.FactName);
        foreach (var contentBlockMarker in check.ContentBlockMarkers)
        {
            DocumentationSourceFacts.ValidateMarkedContentBlock(
                fullDocumentPath,
                check.DocumentPath,
                contentBlockMarker);
        }

        var expectedIdentifiers = ExpectedIdentifiers(rootPath, check);

        if (!documentedIdentifiers.SequenceEqual(expectedIdentifiers, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"Documentation fact check '{check.Name}' is stale in '{check.DocumentPath}'. "
                + $"Expected [{string.Join(", ", expectedIdentifiers)}]; found [{string.Join(", ", documentedIdentifiers)}].");
        }

        ValidateInvocationRequirements(rootPath, check);
    }

    private static string[] ExpectedIdentifiers(string rootPath, DocumentationFactCheck check)
    {
        if (check.ExpectedIdentifiers.Count > 0)
        {
            return check.ExpectedIdentifiers.Order(StringComparer.Ordinal).ToArray();
        }

        if (check.SwitchSources.Count > 0)
        {
            return SwitchIdentifiers(rootPath, check);
        }

        return RegistrationIdentifiers(rootPath, check);
    }

    private static string[] SwitchIdentifiers(string rootPath, DocumentationFactCheck check)
    {
        string[]? expected = null;
        foreach (var source in check.SwitchSources)
        {
            var fullSourcePath = ResolveRepositoryPath(rootPath, source.SourcePath, "C# switch source");
            var identifiers = DocumentationSourceFacts.ReadSwitchCaseTypeNames(
                fullSourcePath,
                source.MethodName,
                source.ParameterCount);
            if (identifiers.Length == 0)
            {
                throw new InvalidOperationException(
                    $"Documentation fact check '{check.Name}' found no switch facts in '{source.SourcePath}'.");
            }

            if (expected is not null && !identifiers.SequenceEqual(expected, StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Documentation fact check '{check.Name}' has mismatched switch facts in '{source.SourcePath}' "
                    + $"for document '{check.DocumentPath}'.");
            }

            expected ??= identifiers;
        }

        return expected ?? [];
    }

    private static string[] RegistrationIdentifiers(string rootPath, DocumentationFactCheck check)
    {
        var identifiers = check.RegistrationSources
            .SelectMany(source => ReadRegistrationIdentifiers(rootPath, source))
            .Where(identifier =>
                check.IncludedIdentifiers.Contains(identifier, StringComparer.Ordinal)
                || check.IncludedIdentifierFragments.Any(fragment => identifier.Contains(fragment, StringComparison.Ordinal)))
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (identifiers.Length == 0)
        {
            throw new InvalidOperationException(
                $"Documentation fact check '{check.Name}' found no registration facts for '{check.DocumentPath}'.");
        }

        return identifiers;
    }

    private static string[] ReadRegistrationIdentifiers(string rootPath, DocumentationSourceMethod source)
    {
        var fullSourcePath = ResolveRepositoryPath(rootPath, source.SourcePath, "C# registration source");
        return string.IsNullOrWhiteSpace(source.MethodName)
            ? DocumentationSourceFacts.ReadTopLevelRegistrationIdentifiers(fullSourcePath)
            : DocumentationSourceFacts.ReadMethodRegistrationIdentifiers(
                fullSourcePath,
                source.MethodName,
                source.ParameterCount);
    }

    private static void ValidateInvocationRequirements(string rootPath, DocumentationFactCheck check)
    {
        foreach (var requirement in check.InvocationRequirements)
        {
            var fullSourcePath = ResolveRepositoryPath(rootPath, requirement.SourcePath, "C# invocation source");
            var identifiers = string.IsNullOrWhiteSpace(requirement.MethodName)
                ? DocumentationSourceFacts.ReadTopLevelInvocationIdentifiers(fullSourcePath)
                : DocumentationSourceFacts.ReadMethodInvocationIdentifiers(
                    fullSourcePath,
                    requirement.MethodName,
                    requirement.ParameterCount);
            var actualCount = identifiers.Count(identifier => identifier == requirement.InvokedMethodName);
            if (actualCount != requirement.ExpectedCount)
            {
                throw new InvalidOperationException(
                    $"Documentation fact check '{check.Name}' expected {requirement.ExpectedCount} invocation(s) of "
                    + $"'{requirement.InvokedMethodName}' in '{requirement.SourcePath}', found {actualCount}.");
            }

            if (requirement.ExpectedArguments.Count == 0)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(requirement.MethodName))
            {
                throw new InvalidOperationException(
                    $"Documentation fact check '{check.Name}' cannot validate top-level invocation arguments in '{requirement.SourcePath}'.");
            }

            var arguments = DocumentationSourceFacts.ReadMethodInvocationArguments(
                fullSourcePath,
                requirement.MethodName,
                requirement.ParameterCount,
                requirement.InvokedMethodName);
            if (!arguments.SequenceEqual(requirement.ExpectedArguments, StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Documentation fact check '{check.Name}' found unexpected arguments for "
                    + $"'{requirement.InvokedMethodName}' in '{requirement.SourcePath}'.");
            }
        }
    }

    private static void ValidateConfig(DocumentationConformanceConfig config, string configPath)
    {
        if (config.Checks is not { Count: > 0 })
        {
            throw new InvalidOperationException($"Documentation conformance config '{configPath}' must contain checks.");
        }

        if (config.Checks.Any(static check => check is null))
        {
            throw new InvalidOperationException($"Documentation conformance config '{configPath}' contains a null check.");
        }

        if (config.Checks.Select(check => check.Name).Distinct(StringComparer.Ordinal).Count() != config.Checks.Count)
        {
            throw new InvalidOperationException($"Documentation conformance config '{configPath}' must contain unique check names.");
        }

        foreach (var check in config.Checks)
        {
            ValidateCheck(check, configPath);
        }
    }

    private static void ValidateCheck(DocumentationFactCheck check, string configPath)
    {
        if (check.ExpectedIdentifiers is null
            || check.ContentBlockMarkers is null
            || check.SwitchSources is null
            || check.RegistrationSources is null
            || check.IncludedIdentifierFragments is null
            || check.IncludedIdentifiers is null
            || check.InvocationRequirements is null)
        {
            throw new InvalidOperationException(
                $"Documentation conformance config '{configPath}' contains null fact check collections.");
        }

        if (check.SwitchSources.Any(static source => source is null)
            || check.RegistrationSources.Any(static source => source is null)
            || check.InvocationRequirements.Any(static requirement => requirement is null || requirement.ExpectedArguments is null))
        {
            throw new InvalidOperationException(
                $"Documentation conformance config '{configPath}' contains a null fact check item.");
        }

        if (string.IsNullOrWhiteSpace(check.Name)
            || string.IsNullOrWhiteSpace(check.DocumentPath)
            || string.IsNullOrWhiteSpace(check.MarkerName)
            || string.IsNullOrWhiteSpace(check.FactName))
        {
            throw new InvalidOperationException(
                $"Documentation conformance config '{configPath}' contains an incomplete fact check.");
        }

        var sourceCount = (check.ExpectedIdentifiers.Count > 0 ? 1 : 0)
            + (check.SwitchSources.Count > 0 ? 1 : 0)
            + (check.RegistrationSources.Count > 0 ? 1 : 0);
        if (sourceCount != 1)
        {
            throw new InvalidOperationException(
                $"Documentation fact check '{check.Name}' must configure exactly one identifier source.");
        }

        if (check.RegistrationSources.Count > 0
            && check.IncludedIdentifiers.Count == 0
            && check.IncludedIdentifierFragments.Count == 0)
        {
            throw new InvalidOperationException(
                $"Documentation registration fact check '{check.Name}' must configure identifier filters.");
        }

        ValidateIdentifiers(check.ExpectedIdentifiers, check.Name, "expected identifiers");
        ValidateIdentifiers(check.ContentBlockMarkers, check.Name, "content block markers");
        ValidateIdentifiers(check.IncludedIdentifiers, check.Name, "included identifiers");
        ValidateIdentifiers(check.IncludedIdentifierFragments, check.Name, "included identifier fragments");
        ValidateSourceMethods(check.SwitchSources, check.Name, requireMethod: true);
        ValidateSourceMethods(check.RegistrationSources, check.Name, requireMethod: false);
        if (check.InvocationRequirements.Any(requirement =>
                string.IsNullOrWhiteSpace(requirement.SourcePath)
                || string.IsNullOrWhiteSpace(requirement.InvokedMethodName)
                || requirement.ParameterCount < 0
                || requirement.ExpectedCount < 0))
        {
            throw new InvalidOperationException(
                $"Documentation fact check '{check.Name}' contains an incomplete invocation requirement.");
        }
    }

    private static void ValidateIdentifiers(List<string> identifiers, string checkName, string description)
    {
        if (identifiers.Any(string.IsNullOrWhiteSpace)
            || identifiers.Distinct(StringComparer.Ordinal).Count() != identifiers.Count)
        {
            throw new InvalidOperationException(
                $"Documentation fact check '{checkName}' must contain unique, non-empty {description}.");
        }
    }

    private static void ValidateSourceMethods(
        List<DocumentationSourceMethod> sources,
        string checkName,
        bool requireMethod)
    {
        if (sources.Any(source =>
                string.IsNullOrWhiteSpace(source.SourcePath)
                || source.ParameterCount < 0
                || (requireMethod && string.IsNullOrWhiteSpace(source.MethodName))))
        {
            throw new InvalidOperationException(
                $"Documentation fact check '{checkName}' contains an incomplete source method.");
        }
    }

    private static string ResolveRepositoryPath(string rootPath, string relativePath, string description)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
        {
            throw new InvalidOperationException($"{description} path '{relativePath}' must be repository-relative.");
        }

        var fullPath = Path.GetFullPath(relativePath, rootPath);
        var resolvedRelativePath = Path.GetRelativePath(rootPath, fullPath);
        if (resolvedRelativePath.Equals("..", StringComparison.Ordinal)
            || resolvedRelativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{description} path '{relativePath}' must stay within the repository root.");
        }

        if (!File.Exists(fullPath))
        {
            throw new InvalidOperationException($"{description} path '{relativePath}' does not exist.");
        }

        EnsureNoSymbolicLinks(rootPath, resolvedRelativePath, description, relativePath);

        return fullPath;
    }

    private static void EnsureNoSymbolicLinks(
        string rootPath,
        string resolvedRelativePath,
        string description,
        string configuredPath)
    {
        var currentPath = rootPath;
        foreach (var segment in resolvedRelativePath.Split(
                     [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            currentPath = Path.Combine(currentPath, segment);
            FileSystemInfo fileSystemInfo = Directory.Exists(currentPath)
                ? new DirectoryInfo(currentPath)
                : new FileInfo(currentPath);
            if (fileSystemInfo.LinkTarget is not null)
            {
                throw new InvalidOperationException(
                    $"{description} path '{configuredPath}' must not contain a symbolic link or junction.");
            }
        }
    }
}
