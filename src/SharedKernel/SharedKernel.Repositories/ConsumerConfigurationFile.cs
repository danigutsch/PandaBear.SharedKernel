using System.Globalization;
using System.Text.Json;
using PandaBear.ConfigurationFiles;
using PandaBear.IO;

namespace PandaBear.Repositories;

/// <summary>
/// Reads and validates the consumer policy in <c>.config/sharedkernel-repo.json</c>. Errors name the file and the JSON
/// path of the problem.
/// </summary>
public static class ConsumerConfigurationFile
{
    /// <summary>
    /// The configuration path relative to the repository root.
    /// </summary>
    public const string RelativePath = ".config/sharedkernel-repo.json";

    private const int MaxPackageIdLength = 100;
    private const int MaxVersionLength = 128;

    /// <summary>
    /// Reads the configuration under <paramref name="root"/> without following symbolic links.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The file is missing, unreadable, larger than 1 MiB, or not a valid configuration of version 1.
    /// </exception>
    internal static async Task<ConsumerConfiguration> Read(string root, CancellationToken cancellationToken)
    {
        string path;
        try
        {
            path = RepositoryPath.Parse(RelativePath).ResolveUnder(Path.GetFullPath(root));
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException($"{RelativePath}: {exception.Message}", exception);
        }

        byte[]? content;
        try
        {
            content = await BoundedReader.ReadFile(path, JsonConfigurationFile.MaxBytes, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            throw new InvalidOperationException($"{RelativePath} does not exist.", exception);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException($"{RelativePath} cannot be read.", exception);
        }

        if (content is null)
        {
            throw new InvalidOperationException($"{RelativePath} exceeds the 1 MiB configuration size limit.");
        }

        return Parse(content);
    }

    /// <summary>
    /// Parses and validates the bytes of a configuration file, such as one read from a commit, and returns its
    /// <c>lockExemptProjects</c>: the repository-relative project files that keep no <c>packages.lock.json</c>.
    /// </summary>
    /// <param name="content">The file content.</param>
    /// <returns>The listed project paths, or an empty list when the section is absent.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="content"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The content is not a valid configuration of version 1.</exception>
    public static IReadOnlyList<string> ParseLockExemptProjects(byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return Parse(content).LockExemptProjects ?? [];
    }

    /// <summary>
    /// Parses and validates the bytes of a configuration file, for callers that read it from somewhere other than the
    /// working tree.
    /// </summary>
    /// <exception cref="InvalidOperationException">The content is not a valid configuration of version 1.</exception>
    internal static ConsumerConfiguration Parse(byte[] content)
    {
        var configuration = JsonConfigurationFile.Parse(content, RelativePath, ConsumerConfigurationJsonContext.Default.ConsumerConfiguration);
        if (configuration.Version != 1)
        {
            throw Invalid("$.version must be 1.");
        }

        if (configuration.LicenseExceptions is { } licenseExceptions)
        {
            Validate(licenseExceptions);
        }

        if (configuration.Links is { } links)
        {
            Validate(links);
        }

        if (configuration.Gherkin?.FindError() is { } gherkinError)
        {
            throw Invalid(gherkinError);
        }

        if (configuration.Labels is { } labels)
        {
            Validate(labels);
        }

        if (configuration.Baseline?.FindError() is { } baselineError)
        {
            throw Invalid(baselineError);
        }

        if (configuration.LockExemptProjects is { } lockExemptProjects)
        {
            ValidateLockExemptProjects(lockExemptProjects);
        }

        if (configuration.TargetFrameworks?.FindError() is { } targetFrameworksError)
        {
            throw Invalid(targetFrameworksError);
        }

        return configuration;
    }

    private static void ValidateLockExemptProjects(IReadOnlyList<string> projects)
    {
        HashSet<string> seen = new(StringComparer.Ordinal);
        for (var index = 0; index < projects.Count; index++)
        {
            var path = string.Create(CultureInfo.InvariantCulture, $"$.lockExemptProjects[{index}]");
            var project = projects[index];
            if (!RepositoryPath.IsValid(project) || !project.EndsWith("proj", StringComparison.Ordinal))
            {
                throw Invalid(path + " must be a repository-relative project file path with '/' separators and without empty, '.', or '..' segments.");
            }

            if (!seen.Add(project))
            {
                throw Invalid(path + " repeats a project.");
            }
        }
    }

    private static void Validate(LinkConfiguration links)
    {
        if (links.Include.Length == 0)
        {
            throw Invalid("$.links.include must name at least one pattern.");
        }

        ValidatePatterns(links.Include, "$.links.include");
        ValidatePatterns(links.Exclude, "$.links.exclude");
    }

    private static void ValidatePatterns(string[] patterns, string path)
    {
        for (var index = 0; index < patterns.Length; index++)
        {
            if (!RepositoryPath.IsValid(patterns[index]))
            {
                throw Invalid(string.Create(CultureInfo.InvariantCulture, $"{path}[{index}] must be a repository-relative path pattern with '/' separators and without empty, '.', or '..' segments."));
            }
        }
    }

    private static void Validate(LicenseExceptionConfiguration licenseExceptions)
    {
        var approval = licenseExceptions.Approval;
        if (!RepositoryPath.IsValid(approval.File))
        {
            throw Invalid("$.licenseExceptions.approval.file must be a repository-relative path with '/' separators and without empty, '.', or '..' segments.");
        }

        if (approval.Values.Count == 0)
        {
            throw Invalid("$.licenseExceptions.approval.values must name at least one property.");
        }

        foreach (var (name, value) in approval.Values)
        {
            var path = "$.licenseExceptions.approval.values" + Key(name);
            if (name.Length == 0)
            {
                throw Invalid(path + " must name a property.");
            }

            if (value.ValueKind is not (JsonValueKind.String or JsonValueKind.True or JsonValueKind.False))
            {
                throw Invalid(path + " must be a string or a boolean.");
            }
        }

        if (licenseExceptions.Packages.Count == 0)
        {
            throw Invalid("$.licenseExceptions.packages must name at least one package.");
        }

        // Package IDs and versions become pkg:nuget URLs in a GitHub Actions output, so neither may hold a separator.
        HashSet<string> packageIds = new(StringComparer.OrdinalIgnoreCase);
        foreach (var (packageId, version) in licenseExceptions.Packages)
        {
            var path = "$.licenseExceptions.packages" + Key(packageId);
            if (!IsPackageId(packageId))
            {
                throw Invalid(path + " must be a NuGet package ID.");
            }

            if (!packageIds.Add(packageId))
            {
                throw Invalid(path + " repeats a package ID that differs only in case.");
            }

            if (!IsVersion(version))
            {
                throw Invalid(path + " must be a NuGet version.");
            }
        }
    }

    // GitHub limits label names to 50 and descriptions to 100 characters, and matches names ignoring case.
    private static void Validate(IReadOnlyList<RepositoryLabel> labels)
    {
        if (labels.Count == 0)
        {
            throw Invalid("$.labels must name at least one label.");
        }

        HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < labels.Count; index++)
        {
            var (name, color, description) = labels[index];
            var path = string.Create(CultureInfo.InvariantCulture, $"$.labels[{index}]");
            if (name.Length is 0 or > 50 || name.Any(char.IsControl) || name.Trim().Length != name.Length)
            {
                throw Invalid(path + ".name must be 1 to 50 characters without control characters or surrounding white space.");
            }

            if (!names.Add(name))
            {
                throw Invalid(path + ".name repeats a label name that differs only in case.");
            }

            if (color.Length != 6 || !color.All(char.IsAsciiHexDigit))
            {
                throw Invalid(path + ".color must be six hexadecimal digits without '#'.");
            }

            if (description.Length > 100 || description.Any(char.IsControl))
            {
                throw Invalid(path + ".description must be at most 100 characters without control characters.");
            }
        }
    }

    private static bool IsPackageId(string value) =>
        value.Length is > 0 and <= MaxPackageIdLength
        && value.All(static character => char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or '_');

    // A resolved lock file version: release digits and an optional prerelease label, without build metadata.
    private static bool IsVersion(string? value) =>
        value is { Length: > 0 and <= MaxVersionLength }
        && char.IsAsciiDigit(value[0])
        && value.All(static character => char.IsAsciiLetterOrDigit(character) || character is '.' or '-');

    private static string Key(string name) => "['" + name.Replace("'", "\\'", StringComparison.Ordinal) + "']";

    private static InvalidOperationException Invalid(string message) => new($"{RelativePath}: {message}");
}
