using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using PandaBear.Tooling;

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

    private const int MaxBytes = 1024 * 1024;

    private const int MaxTimeoutSeconds = 3600;

    private const string RelativePathRule = "with '/' separators and without empty, '.', or '..' segments.";

    /// <summary>The variables that never reach a child process, because they hold a GitHub token.</summary>
    private static readonly string[] TokenVariables = ["GH_TOKEN", "GITHUB_TOKEN"];

    /// <summary>Reads the configuration under <paramref name="root"/> without following symbolic links.</summary>
    /// <exception cref="InvalidOperationException">
    /// The file is missing, unreadable, larger than 1 MiB, or not a valid configuration of version 1.
    /// </exception>
    public static async Task<K6Configuration> Read(string root, CancellationToken ct)
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
            content = await ReadBounded(path, ct).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            throw new InvalidOperationException($"{RelativePath} does not exist.", exception);
        }

        K6Configuration? configuration;
        try
        {
            configuration = JsonSerializer.Deserialize(content, K6ConfigurationJsonContext.Default.K6Configuration);
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

    /// <summary>Reports whether <paramref name="image"/> is an image reference pinned by a SHA-256 digest.</summary>
    public static bool IsDigestPinned(string image) => DigestPinnedImage().IsMatch(image);

    /// <summary>Creates the error for a value at a JSON path of the file.</summary>
    public static InvalidOperationException Invalid(string message) => new($"{RelativePath}: {message}");

    private static async Task<byte[]> ReadBounded(string path, CancellationToken ct)
    {
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
        await using (stream.ConfigureAwait(false))
        {
            var buffer = new byte[MaxBytes + 1];
            var length = 0;
            int read;
            while (length < buffer.Length && (read = await stream.ReadAsync(buffer.AsMemory(length), ct).ConfigureAwait(false)) > 0)
            {
                length += read;
            }

            if (length > MaxBytes)
            {
                throw new InvalidOperationException($"{RelativePath} is larger than 1 MiB.");
            }

            return buffer[..length];
        }
    }

    private static void Validate(K6Configuration configuration)
    {
        if (configuration.Version != 1)
        {
            throw Invalid("$.version must be 1.");
        }

        if (!RepositoryPath.IsRelative(configuration.ScriptDirectory))
        {
            throw Invalid("$.scriptDirectory must be a repository-relative path " + RelativePathRule);
        }

        if (!RepositoryPath.IsRelative(configuration.ResultsDirectory))
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

            if (!RepositoryPath.IsRelative(scenario.Script))
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

        if (TokenVariables.Contains(name, StringComparer.OrdinalIgnoreCase))
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
