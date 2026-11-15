using System.Globalization;
using System.Text.RegularExpressions;
using PandaBear.ConfigurationFiles;
using PandaBear.GitHub;
using PandaBear.IO;

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

    /// <summary>Reads the configuration under <paramref name="root"/> without following symbolic links.</summary>
    /// <exception cref="InvalidOperationException">
    /// The file is missing, unreadable, larger than 1 MiB, or not a valid configuration of version 1.
    /// </exception>
    public static async Task<CiConfiguration> Read(string root, CancellationToken ct)
    {
        var configuration = await JsonConfigurationFile.Read(root, RelativePath, CiConfigurationJsonContext.Default.CiConfiguration, ct).ConfigureAwait(false);
        Validate(configuration);
        return configuration;
    }

    private static void Validate(CiConfiguration configuration)
    {
        if (configuration.Version != 1)
        {
            throw Invalid("$.version must be 1.");
        }

        if (!RepositoryPath.IsValid(configuration.Solution) || !configuration.Solution.EndsWith(".slnx", StringComparison.Ordinal))
        {
            throw Invalid("$.solution must be a repository-relative .slnx path with '/' separators and without empty, '.', or '..' segments.");
        }

        var selection = configuration.Selection;
        ValidatePatterns(selection.FullValidation, "$.selection.fullValidation");
        ValidatePatterns(selection.Documentation, "$.selection.documentation");
        ValidateProjects(selection.AlwaysSelected, "$.selection.alwaysSelected", new Dictionary<string, string>(StringComparer.Ordinal));
        ValidateTestPaths(selection.TestPaths ?? []);

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

            if (!RepositoryPath.IsValid(target.Project))
            {
                throw Invalid(path + ".project must be a repository-relative project path with '/' separators and without empty, '.', or '..' segments.");
            }

            // The property reaches dotnet build as -p:<name>=true, so it cannot carry another property or a switch.
            ValidateProperty(target.GenerateProperty, path + ".generateProperty");
            ValidateProperty(target.RefreshProperty, path + ".refreshProperty");
            ValidatePreservedEnvironment(target.PreserveEnvironment ?? [], path + ".preserveEnvironment");
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

        // A service is checked after every slice, because a later slice can list the project as a test project.
        var handedVariables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < configuration.Slices.Length; index++)
        {
            if (configuration.Slices[index].Service is { } service)
            {
                ValidateService(service, "$.slices[" + index.ToString(CultureInfo.InvariantCulture) + "].service", configuration.Slices[index].Name, owners, handedVariables);
            }
        }

        if (configuration.DefaultSlice is { } defaultSlice && !names.Contains(defaultSlice))
        {
            throw Invalid("$.defaultSlice must name a slice in $.slices.");
        }

        if (configuration.Coverage is { } coverage)
        {
            ValidateCoverage(coverage);
        }

        if (configuration.Mutation is { } mutation)
        {
            ValidateMutation(mutation);
        }
    }

    private static void ValidateService(
        ServiceConfiguration service, string path, string slice, Dictionary<string, string> testProjects, Dictionary<string, string> handedVariables)
    {
        if (!RepositoryPath.IsValid(service.Project))
        {
            throw Invalid(path + ".project must be a repository-relative project path with '/' separators and without empty, '.', or '..' segments.");
        }

        if (testProjects.TryGetValue(service.Project, out var owner))
        {
            throw Invalid(path + ".project names " + service.Project + ", which slice " + owner + " lists as a test project; a service is not a test project.");
        }

        ValidateVariable(service.ReadyFileVariable, path + ".readyFileVariable");
        ValidateVariable(service.ValueVariable, path + ".valueVariable");
        if (string.Equals(service.ValueVariable, service.ReadyFileVariable, StringComparison.OrdinalIgnoreCase))
        {
            throw Invalid(path + ".valueVariable repeats readyFileVariable " + service.ReadyFileVariable + ".");
        }

        // The test run of a run that merges slices has one environment, so two services cannot hand over the same variable.
        if (!handedVariables.TryAdd(service.ValueVariable, slice))
        {
            throw Invalid(path + ".valueVariable repeats " + service.ValueVariable + ", which the service of slice " + handedVariables[service.ValueVariable] + " already hands to its tests.");
        }

        ValidateTimeout(service.ReadyTimeoutSeconds, path + ".readyTimeoutSeconds");
        ValidateTimeout(service.StopTimeoutSeconds, path + ".stopTimeoutSeconds");
    }

    private static void ValidateVariable(string name, string path)
    {
        if (!Identifier().IsMatch(name))
        {
            throw Invalid(path + " must be an environment variable name: letters, digits, and '_', not starting with a digit.");
        }

        if (GitHubToken.IsTokenVariable(name))
        {
            throw Invalid(path + " names " + name + ", a GitHub token variable that the tool removes from the service's environment; use another name.");
        }
    }

    private static void ValidateTimeout(int seconds, string path)
    {
        if (seconds is < 1 or > ServiceConfiguration.MaxTimeoutSeconds)
        {
            throw Invalid(path + " must be from 1 to " + ServiceConfiguration.MaxTimeoutSeconds.ToString(CultureInfo.InvariantCulture) + ".");
        }
    }

    private static void ValidateMutation(MutationConfiguration mutation)
    {
        // The version reaches dotnet tool install as --version <value> and names the scratch tool directory.
        if (!ExactVersion().IsMatch(mutation.StrykerVersion))
        {
            throw Invalid("$.mutation.strykerVersion must be an exact NuGet version, such as 4.15.0.");
        }

        if (mutation.ConfigFiles.Length == 0)
        {
            throw Invalid("$.mutation.configFiles must name at least one Stryker config file.");
        }

        var configFiles = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < mutation.ConfigFiles.Length; index++)
        {
            var path = "$.mutation.configFiles[" + index.ToString(CultureInfo.InvariantCulture) + "]";
            var configFile = mutation.ConfigFiles[index];
            if (configFile is null || !RepositoryPath.IsValid(configFile))
            {
                throw Invalid(path + " must be a repository-relative file path with '/' separators and without empty, '.', or '..' segments.");
            }

            if (!configFiles.Add(configFile))
            {
                throw Invalid(path + " repeats " + configFile + ".");
            }
        }
    }

    private static void ValidateCoverage(CoverageConfiguration coverage)
    {
        if (coverage.ThresholdPercent is < 0 or > 100)
        {
            throw Invalid("$.coverage.thresholdPercent must be from 0 to 100.");
        }

        if (coverage.Include.Length == 0)
        {
            throw Invalid("$.coverage.include must name at least one pattern.");
        }

        ValidatePatterns(coverage.Include, "$.coverage.include");
        ValidatePatterns(coverage.Exclude, "$.coverage.exclude");
        if (coverage.SymbolDirectories.Length == 0)
        {
            throw Invalid("$.coverage.symbolDirectories must name at least one directory.");
        }

        for (var index = 0; index < coverage.SymbolDirectories.Length; index++)
        {
            if (!RepositoryPath.IsValid(coverage.SymbolDirectories[index]))
            {
                throw Invalid("$.coverage.symbolDirectories[" + index.ToString(CultureInfo.InvariantCulture)
                    + "] must be a repository-relative directory with '/' separators and without empty, '.', or '..' segments.");
            }
        }
    }

    private static void ValidateTestPaths(TestPathRule[] rules)
    {
        for (var index = 0; index < rules.Length; index++)
        {
            var rule = rules[index];
            var path = "$.selection.testPaths[" + index.ToString(CultureInfo.InvariantCulture) + "]";
            if (rule is null)
            {
                throw Invalid(path + " must be an object.");
            }

            if (rule.Paths.Length == 0)
            {
                throw Invalid(path + ".paths must name at least one pattern.");
            }

            ValidatePatterns(rule.Paths, path + ".paths");
            if (rule.Tests.Length == 0)
            {
                throw Invalid(path + ".tests must name at least one test project.");
            }

            ValidateProjects(rule.Tests, path + ".tests", new Dictionary<string, string>(StringComparer.Ordinal));
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
            if (project is null || !RepositoryPath.IsValid(project))
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

    private static void ValidatePreservedEnvironment(string[] names, string path)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < names.Length; index++)
        {
            if (names[index] is null || !Identifier().IsMatch(names[index]))
            {
                throw Invalid(path + "[" + index.ToString(CultureInfo.InvariantCulture) + "] must be an environment variable name.");
            }

            // The minimal environment exists to keep credentials out of the build, so a list cannot bring them back.
            if (CredentialName().IsMatch(names[index]))
            {
                throw Invalid(path + "[" + index.ToString(CultureInfo.InvariantCulture) + "] must not name a credential variable.");
            }

            // A loader or startup hook runs code in the build, which the minimal environment also keeps out.
            if (LoaderName().IsMatch(names[index]))
            {
                throw Invalid(path + "[" + index.ToString(CultureInfo.InvariantCulture) + "] must not name a loader or runtime-hook variable.");
            }

            if (!seen.Add(names[index]))
            {
                throw Invalid(path + " repeats " + names[index] + ".");
            }
        }
    }

    private static void ValidateProperty(string value, string path)
    {
        if (!Identifier().IsMatch(value))
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

    [GeneratedRegex("TOKEN|SECRET|PASS|AUTH|KEY|CREDENTIAL|PRIVATE|CERT|CONNECTION|SAS|SIGNATURE", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex CredentialName();

    [GeneratedRegex(@"\A(?:LD_|DYLD_|DOTNET_STARTUP_HOOKS\z)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex LoaderName();

    // An MSBuild property name and an environment variable name share this shape.
    [GeneratedRegex(@"\A[A-Za-z_][A-Za-z0-9_]*\z", RegexOptions.CultureInvariant)]
    private static partial Regex Identifier();

    [GeneratedRegex(@"\A[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?\z", RegexOptions.CultureInvariant)]
    private static partial Regex ExactVersion();
}
