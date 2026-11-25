using System.Globalization;
using System.Text.RegularExpressions;
using SharedKernel.ConfigurationFiles;
using SharedKernel.IO;
using SharedKernel.Processes;

namespace SharedKernel.LoadTesting;

/// <summary>
/// Reads and validates a load-test configuration file. Errors name the file and the JSON path of the problem.
/// </summary>
/// <param name="relativePath">The configuration path relative to the repository root, such as <c>.config/sharedkernel-k6.json</c>.</param>
/// <param name="reservedVariables">The variables that the load generator sets, which the scenarios cannot name.</param>
internal sealed partial class LoadTestConfigurationFile(string relativePath, IReadOnlyCollection<string> reservedVariables)
{
    private const int MaxTimeoutSeconds = 3600;

    private const string RelativePathRule = "with '/' separators and without empty, '.', or '..' segments.";

    /// <summary>Gets the configuration path relative to the repository root.</summary>
    public string RelativePath => relativePath;

    /// <summary>Reads the configuration under <paramref name="root"/> without following symbolic links.</summary>
    /// <exception cref="InvalidOperationException">
    /// The file is missing, unreadable, larger than 1 MiB, or not a valid configuration of version 1.
    /// </exception>
    public async Task<LoadTestConfiguration> Read(string root, CancellationToken ct)
    {
        var configuration = await JsonConfigurationFile.Read(root, relativePath, LoadTestConfigurationJsonContext.Default.LoadTestConfiguration, ct).ConfigureAwait(false);
        Validate(configuration);
        return configuration;
    }

    /// <summary>Creates the error for a value at a JSON path of the file.</summary>
    public InvalidOperationException Invalid(string message) => new($"{relativePath}: {message}");

    private void Validate(LoadTestConfiguration configuration)
    {
        if (configuration.Version != 1)
        {
            throw Invalid("$.version must be 1.");
        }

        if (!RepositoryPath.IsValid(configuration.ScriptDirectory))
        {
            throw Invalid("$.scriptDirectory must be a repository-relative path " + RelativePathRule);
        }

        if (!RepositoryPath.IsValid(configuration.ResultsDirectory))
        {
            throw Invalid("$.resultsDirectory must be a repository-relative path " + RelativePathRule);
        }

        if (configuration.Container is { } container)
        {
            if (container.Engine is not ("docker" or "podman"))
            {
                throw Invalid("$.container.engine must be docker or podman.");
            }

            if (!LoadTestContainer.IsDigestPinned(container.Image))
            {
                throw Invalid("$.container.image must be an image reference pinned by digest, such as grafana/k6:<tag>@sha256:<digest>.");
            }
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < configuration.Scenarios.Length; index++)
        {
            var path = "$.scenarios[" + Index(index) + "]";
            var scenario = configuration.Scenarios[index] ?? throw Invalid(path + " must be an object.");
            if (!ScenarioName().IsMatch(scenario.Name))
            {
                throw Invalid(path + ".name must be lowercase letters and digits in words separated by '-'.");
            }

            if (!names.Add(scenario.Name))
            {
                throw Invalid(path + ".name repeats the scenario " + scenario.Name + ".");
            }

            if (!RepositoryPath.IsValid(scenario.Script))
            {
                throw Invalid(path + ".script must be a path relative to $.scriptDirectory " + RelativePathRule);
            }

            ValidateVariable(scenario.BaseUrlVariable, path + ".baseUrlVariable");
            var variables = new HashSet<string>(StringComparer.Ordinal) { scenario.BaseUrlVariable };
            var environment = scenario.Environment ?? [];
            for (var variable = 0; variable < environment.Length; variable++)
            {
                var variablePath = path + ".environment[" + Index(variable) + "]";
                var name = environment[variable] ?? throw Invalid(variablePath + " must be a string.");
                ValidateVariable(name, variablePath);
                if (!variables.Add(name))
                {
                    throw Invalid(variablePath + " repeats " + name + ".");
                }
            }

            if (scenario.Host is { } host)
            {
                ValidateHost(host, path + ".host");
            }
        }
    }

    private void ValidateHost(HostConfiguration host, string path)
    {
        if (!CommandName().IsMatch(host.Command))
        {
            throw Invalid(path + ".command must be a command name: letters, digits, '.', '_', and '-', without a path.");
        }

        for (var index = 0; index < host.Arguments.Length; index++)
        {
            if (host.Arguments[index] is null)
            {
                throw Invalid(path + ".arguments[" + Index(index) + "] must be a string.");
            }
        }

        ValidateVariable(host.ReadyFileVariable, path + ".readyFileVariable");
        if (!HealthPath().IsMatch(host.HealthPath))
        {
            throw Invalid(path + ".healthPath must be a path that starts with '/', without a query or fragment.");
        }

        if (host.ReadyTimeoutSeconds is < 1 or > MaxTimeoutSeconds)
        {
            throw Invalid(path + ".readyTimeoutSeconds must be from 1 to 3600.");
        }

        if (host.HealthTimeoutSeconds is < 1 or > MaxTimeoutSeconds)
        {
            throw Invalid(path + ".healthTimeoutSeconds must be from 1 to 3600.");
        }
    }

    private void ValidateVariable(string name, string path)
    {
        if (!VariableName().IsMatch(name))
        {
            throw Invalid(path + " must be an environment variable name: letters, digits, and '_', not starting with a digit.");
        }

        if (ProcessEnvironment.IsForgeTokenVariable(name))
        {
            throw Invalid(path + " names " + name + ", which the tool never passes to a child process.");
        }

        if (reservedVariables.Contains(name, StringComparer.Ordinal))
        {
            throw Invalid(path + " names " + name + ", which the tool sets.");
        }
    }

    private static string Index(int index) => index.ToString(CultureInfo.InvariantCulture);

    [GeneratedRegex(@"\A[a-z0-9]+(?:-[a-z0-9]+)*\z", RegexOptions.CultureInvariant)]
    private static partial Regex ScenarioName();

    [GeneratedRegex(@"\A[A-Za-z_][A-Za-z0-9_]*\z", RegexOptions.CultureInvariant)]
    private static partial Regex VariableName();

    [GeneratedRegex(@"\A[A-Za-z0-9][A-Za-z0-9._-]*\z", RegexOptions.CultureInvariant)]
    private static partial Regex CommandName();

    [GeneratedRegex(@"\A/(?:[A-Za-z0-9._~-]+/?)*\z", RegexOptions.CultureInvariant)]
    private static partial Regex HealthPath();
}
