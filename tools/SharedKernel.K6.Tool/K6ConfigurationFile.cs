using System.Globalization;
using System.Text.RegularExpressions;
using PandaBear.ConfigurationFiles;
using PandaBear.GitHub;
using PandaBear.IO;

namespace PandaBear.K6.Tool;

/// <summary>
/// Reads and validates <c>.config/sharedkernel-k6.json</c>. Errors name the file and the JSON path of the problem.
/// </summary>
internal static partial class K6ConfigurationFile
{
    /// <summary>The configuration path relative to the repository root.</summary>
    public const string RelativePath = ".config/sharedkernel-k6.json";

    /// <summary>The k6 variable that the tool sets so k6 sends no usage report.</summary>
    public const string UsageReportVariable = "K6_NO_USAGE_REPORT";

    private const int MaxTimeoutSeconds = 3600;

    private const string RelativePathRule = "with '/' separators and without empty, '.', or '..' segments.";

    /// <summary>Reads the configuration under <paramref name="root"/> without following symbolic links.</summary>
    /// <exception cref="InvalidOperationException">
    /// The file is missing, unreadable, larger than 1 MiB, or not a valid configuration of version 1.
    /// </exception>
    public static async Task<K6Configuration> Read(string root, CancellationToken ct)
    {
        var configuration = await JsonConfigurationFile.Read(root, RelativePath, K6ConfigurationJsonContext.Default.K6Configuration, ct).ConfigureAwait(false);
        Validate(configuration);
        return configuration;
    }

    /// <summary>Reports whether <paramref name="image"/> is an image reference pinned by a SHA-256 digest.</summary>
    public static bool IsDigestPinned(string image) => DigestPinnedImage().IsMatch(image);

    /// <summary>Creates the error for a value at a JSON path of the file.</summary>
    public static InvalidOperationException Invalid(string message) => new($"{RelativePath}: {message}");

    private static void Validate(K6Configuration configuration)
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

            if (!IsDigestPinned(container.Image))
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

    private static void ValidateHost(K6HostConfiguration host, string path)
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

    private static void ValidateVariable(string name, string path)
    {
        if (!VariableName().IsMatch(name))
        {
            throw Invalid(path + " must be an environment variable name: letters, digits, and '_', not starting with a digit.");
        }

        if (GitHubToken.IsTokenVariable(name))
        {
            throw Invalid(path + " names " + name + ", which the tool never passes to a child process.");
        }

        if (string.Equals(name, UsageReportVariable, StringComparison.Ordinal))
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

    // The reference starts with a lowercase letter or digit, so it can never read as an engine option.
    [GeneratedRegex(@"\A[a-z0-9][^\s@]*@sha256:[0-9a-f]{64}\z", RegexOptions.CultureInvariant)]
    private static partial Regex DigestPinnedImage();
}
