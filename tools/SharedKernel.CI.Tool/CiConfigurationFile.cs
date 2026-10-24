using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using PandaBear.Tooling;

namespace PandaBear.CI.Tool;

/// <summary>
/// Reads and validates <c>.config/sharedkernel-ci.json</c>. Errors name the file and the JSON path of the problem.
/// </summary>
internal static partial class CiConfigurationFile
{
    /// <summary>The configuration path relative to the repository root.</summary>
    public const string RelativePath = ".config/sharedkernel-ci.json";

    /// <summary>The file name that <c>select</c> reserves for the filter of the selected non-test projects.</summary>
    public const string BuildFilterName = "build";

    private const int MaxBytes = 1024 * 1024;

    /// <summary>Reads the configuration under <paramref name="root"/> without following symbolic links.</summary>
    /// <exception cref="InvalidOperationException">
    /// The file is missing, unreadable, larger than 1 MiB, or not a valid configuration of version 1.
    /// </exception>
    public static async Task<CiConfiguration> Read(string root, CancellationToken ct)
    {
        string path;
        try
        {
            path = RepositoryPath.Resolve(root, RelativePath);
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException($"{RelativePath}: {exception.Message}", exception);
        }

        byte[] content;
        try
        {
            content = await BoundedFile.Read(path, MaxBytes, RelativePath, ct).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            throw new InvalidOperationException($"{RelativePath} does not exist.", exception);
        }

        CiConfiguration? configuration;
        try
        {
            configuration = JsonSerializer.Deserialize(content, CiConfigurationJsonContext.Default.CiConfiguration);
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

        Validate(configuration);
        return configuration;
    }

    private static void Validate(CiConfiguration configuration)
    {
        if (configuration.Version != 1)
        {
            throw Invalid("$.version must be 1.");
        }

        if (!RepositoryPath.IsRelative(configuration.Solution) || !configuration.Solution.EndsWith(".slnx", StringComparison.Ordinal))
        {
            throw Invalid("$.solution must be a repository-relative .slnx path with '/' separators and without empty, '.', or '..' segments.");
        }

        var selection = configuration.Selection;
        ValidatePatterns(selection.FullValidation, "$.selection.fullValidation");
        ValidatePatterns(selection.Documentation, "$.selection.documentation");
        ValidateProjects(selection.AlwaysSelected, "$.selection.alwaysSelected", new Dictionary<string, string>(StringComparer.Ordinal));

        if (configuration.Slices.Length == 0)
        {
            throw Invalid("$.slices must name at least one slice.");
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        var owners = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < configuration.Slices.Length; index++)
        {
            var slice = configuration.Slices[index];
            var path = "$.slices[" + index.ToString(CultureInfo.InvariantCulture) + "]";
            if (slice is null)
            {
                throw Invalid(path + " must be an object.");
            }

            if (!IsSliceName(slice.Name))
            {
                throw Invalid(path + ".name must be lowercase letters and digits in words separated by '-', and not 'build'.");
            }

            if (!names.Add(slice.Name))
            {
                throw Invalid(path + ".name repeats the slice " + slice.Name + ".");
            }

            if (slice.Projects.Length == 0 && slice.Name != configuration.DefaultSlice)
            {
                throw Invalid(path + ".projects must name at least one test project unless the slice is the default slice.");
            }

            ValidateProjects(slice.Projects, path + ".projects", owners, slice.Name);
            if (slice.OpenApi is { } openApi)
            {
                ValidatePatterns(openApi, path + ".openApi");
                if (openApi.Distinct(StringComparer.Ordinal).Count() != openApi.Length)
                {
                    throw Invalid(path + ".openApi repeats a target.");
                }
            }
        }

        if (configuration.DefaultSlice is { } defaultSlice && !names.Contains(defaultSlice))
        {
            throw Invalid("$.defaultSlice must name a slice in $.slices.");
        }
    }

    private static void ValidatePatterns(string[] values, string path)
    {
        for (var index = 0; index < values.Length; index++)
        {
            if (string.IsNullOrWhiteSpace(values[index]) || values[index].Contains('\\', StringComparison.Ordinal))
            {
                throw Invalid(path + "[" + index.ToString(CultureInfo.InvariantCulture) + "] must be a non-empty value with '/' separators.");
            }
        }
    }

    private static void ValidateProjects(string[] projects, string path, Dictionary<string, string> owners, string? slice = null)
    {
        for (var index = 0; index < projects.Length; index++)
        {
            var itemPath = path + "[" + index.ToString(CultureInfo.InvariantCulture) + "]";
            var project = projects[index];
            if (project is null || !RepositoryPath.IsRelative(project))
            {
                throw Invalid(itemPath + " must be a repository-relative project path with '/' separators and without empty, '.', or '..' segments.");
            }

            if (!owners.TryAdd(project, slice ?? string.Empty))
            {
                throw Invalid(slice is null
                    ? itemPath + " repeats " + project + "."
                    : itemPath + " names " + project + ", which slice " + owners[project] + " already lists; a test project belongs to one slice.");
            }
        }
    }

    /// <summary>Reports whether <paramref name="name"/> is a valid slice name.</summary>
    public static bool IsSliceName(string name) => SliceName().IsMatch(name) && name != BuildFilterName;

    /// <summary>Creates the error for a value at a JSON path of the file.</summary>
    public static InvalidOperationException Invalid(string message) => new($"{RelativePath}: {message}");

    [GeneratedRegex(@"\A[a-z0-9]+(?:-[a-z0-9]+)*\z", RegexOptions.CultureInvariant)]
    private static partial Regex SliceName();
}
