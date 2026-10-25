using System.Globalization;
using System.Text.Json;
using PandaBear.Tooling;

namespace PandaBear.RepoConfig.Tool;

/// <summary>
/// Reads and validates the consumer policy in <c>.config/sharedkernel-repo.json</c>. Errors name the file and the JSON
/// path of the problem.
/// </summary>
internal static class ConsumerConfigurationFile
{
    /// <summary>
    /// The configuration path relative to the repository root.
    /// </summary>
    public const string RelativePath = ".config/sharedkernel-repo.json";

    private const int MaxBytes = 1024 * 1024;
    private const int MaxPackageIdLength = 100;
    private const int MaxVersionLength = 128;

    /// <summary>
    /// Reads the configuration under <paramref name="root"/> without following symbolic links.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The file is missing, unreadable, larger than 1 MiB, or not a valid configuration of version 1.
    /// </exception>
    public static async Task<ConsumerConfiguration> Read(string root, CancellationToken cancellationToken)
    {
        string path;
        try
        {
            path = RepositoryPath.Resolve(Path.GetFullPath(root), RelativePath);
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException($"{RelativePath}: {exception.Message}", exception);
        }

        byte[]? content;
        try
        {
            content = await RepositoryFile.Read(path, MaxBytes, cancellationToken).ConfigureAwait(false);
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

        ConsumerConfiguration? configuration;
        try
        {
            configuration = JsonSerializer.Deserialize(
                RepositoryFile.WithoutUtf8ByteOrderMark(content).Span,
                ConsumerConfigurationJsonContext.Default.ConsumerConfiguration);
        }
        catch (JsonException exception)
        {
            // The serializer's message names .NET types, so only the location is reported.
            throw new InvalidOperationException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{RelativePath} is not valid at {exception.Path ?? "$"} (line {(exception.LineNumber ?? 0) + 1}). Check for a misspelled, unknown, missing, or repeated property, a value of the wrong type, or broken JSON syntax."),
                exception);
        }

        if (configuration is null)
        {
            throw new InvalidOperationException($"{RelativePath} must contain a JSON object.");
        }

        if (configuration.Version != 1)
        {
            throw Invalid("$.version must be 1.");
        }

        if (configuration.LicenseExceptions is { } licenseExceptions)
        {
            Validate(licenseExceptions);
        }

        if (configuration.Labels is { } labels)
        {
            Validate(labels);
        }

        return configuration;
    }

    private static void Validate(LicenseExceptionConfiguration licenseExceptions)
    {
        var approval = licenseExceptions.Approval;
        if (!RepositoryPath.IsRelative(approval.File))
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
    private static void Validate(IReadOnlyList<GitHubLabelConfiguration> labels)
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
