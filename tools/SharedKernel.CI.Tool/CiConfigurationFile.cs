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

        var targets = new HashSet<string>(StringComparer.Ordinal);
        var openApi = configuration.OpenApi ?? [];
        for (var index = 0; index < openApi.Length; index++)
        {
            var target = openApi[index];
            var path = "$.openApi[" + index.ToString(CultureInfo.InvariantCulture) + "]";
            if (target is null)
            {
                throw Invalid(path + " must be an object.");
            }

            if (!IsName(target.Name))
            {
                throw Invalid(path + ".name must be lowercase letters and digits in words separated by '-'.");
            }

            if (!targets.Add(target.Name))
            {
                throw Invalid(path + ".name repeats the OpenAPI target " + target.Name + ".");
            }

            if (!RepositoryPath.IsRelative(target.Project))
            {
                throw Invalid(path + ".project must be a repository-relative project path with '/' separators and without empty, '.', or '..' segments.");
            }

            // The property reaches dotnet build as -p:<name>=true, so it cannot carry another property or a switch.
            ValidateProperty(target.GenerateProperty, path + ".generateProperty");
            ValidateProperty(target.RefreshProperty, path + ".refreshProperty");
        }

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
            var sliceTargets = slice.OpenApi ?? [];
            for (var target = 0; target < sliceTargets.Length; target++)
            {
                if (!targets.Contains(sliceTargets[target] ?? string.Empty))
                {
                    throw Invalid(path + ".openApi[" + target.ToString(CultureInfo.InvariantCulture) + "] names " + sliceTargets[target] + ", which $.openApi does not define.");
                }
            }

            if (sliceTargets.Distinct(StringComparer.Ordinal).Count() != sliceTargets.Length)
            {
                throw Invalid(path + ".openApi repeats a target.");
            }

            if (slice.Playwright is { } playwright)
            {
                if (!slice.Projects.Contains(playwright.Project, StringComparer.Ordinal))
                {
                    throw Invalid(path + ".playwright.project must name a test project that the slice lists.");
                }

                if (playwright.Browsers.Length == 0)
                {
                    throw Invalid(path + ".playwright.browsers must name at least one browser.");
                }

                for (var browser = 0; browser < playwright.Browsers.Length; browser++)
                {
                    // Browsers follow the install command, so a value that starts with '-' would be an option.
                    if (!Browser().IsMatch(playwright.Browsers[browser] ?? string.Empty))
                    {
                        throw Invalid(path + ".playwright.browsers[" + browser.ToString(CultureInfo.InvariantCulture)
                            + "] must be lowercase letters, digits, and '-', starting with a letter.");
                    }
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
    public static bool IsSliceName(string name) => IsName(name) && name != BuildFilterName;

    /// <summary>Reports whether <paramref name="name"/> is lowercase letters and digits in words separated by <c>-</c>.</summary>
    public static bool IsName(string name) => SliceName().IsMatch(name);

    /// <summary>Reports whether <paramref name="browser"/> is a Playwright browser name: lowercase letters, digits, and <c>-</c>, starting with a letter.</summary>
    public static bool IsBrowser(string browser) => Browser().IsMatch(browser);

    private static void ValidateProperty(string value, string path)
    {
        if (!Property().IsMatch(value))
        {
            throw Invalid(path + " must be an MSBuild property name.");
        }
    }

    /// <summary>Creates the error for a value at a JSON path of the file.</summary>
    public static InvalidOperationException Invalid(string message) => new($"{RelativePath}: {message}");

    [GeneratedRegex(@"\A[a-z0-9]+(?:-[a-z0-9]+)*\z", RegexOptions.CultureInvariant)]
    private static partial Regex SliceName();

    [GeneratedRegex(@"\A[a-z][a-z0-9-]*\z", RegexOptions.CultureInvariant)]
    private static partial Regex Browser();

    [GeneratedRegex(@"\A[A-Za-z_][A-Za-z0-9_]*\z", RegexOptions.CultureInvariant)]
    private static partial Regex Property();
}
