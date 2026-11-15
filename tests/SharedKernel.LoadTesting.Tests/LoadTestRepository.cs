using System.Globalization;

namespace PandaBear.LoadTesting.Tests;

/// <summary>A temporary repository with a load-test configuration and a script.</summary>
internal sealed class LoadTestRepository : IDisposable
{
    /// <summary>The configuration path relative to the root.</summary>
    public const string ConfigurationPath = ".config/load.json";

    /// <summary>A digest-pinned image reference.</summary>
    public const string Image = "registry.example/gen:1.0@sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    /// <summary>A configuration with a <c>smoke</c> scenario and a docker container section.</summary>
    public const string DefaultConfiguration = $$"""
        {
          "version": 1,
          "scriptDirectory": "perf/scripts",
          "resultsDirectory": "perf/results",
          "container": { "engine": "docker", "image": "{{Image}}" },
          "scenarios": [
            {
              "name": "smoke",
              "script": "scenarios/smoke.js",
              "baseUrlVariable": "APP_BASE_URL",
              "environment": ["APP_PROFILE", "APP_VUS"]
            }
          ]
        }
        """;

    private readonly TemporaryDirectory _directory = new();

    /// <summary>Creates the repository layout with a smoke script and writes <c>configuration</c>.</summary>
    public LoadTestRepository(string configuration = DefaultConfiguration)
    {
        Directory.CreateDirectory(Path.Combine(Root, ".config"));
        Directory.CreateDirectory(Path.Combine(Root, "perf", "scripts", "scenarios"));
        File.WriteAllText(Path.Combine(Root, "perf", "scripts", "scenarios", "smoke.js"), "export default function () {}\n");
        File.WriteAllText(Path.Combine(Root, ".config", "load.json"), configuration);
    }

    /// <summary>Gets the root directory.</summary>
    public string Root => _directory.Path;

    /// <summary>Gets the results directory.</summary>
    public string ResultsDirectory => Path.Combine(Root, "perf", "results");

    /// <summary>Gets the executables that the runner resolves.</summary>
    public MappedExecutableResolver Executables { get; } = new();

    /// <summary>Gets what the runner writes to its diagnostics.</summary>
    public StringWriter Error { get; } = new();

    /// <summary>Builds a configuration whose <c>upload</c> scenario has a host command <c>app-host</c>.</summary>
    public static string HostConfiguration(int healthTimeoutSeconds = 10) => $$"""
        {
          "version": 1,
          "scriptDirectory": "perf/scripts",
          "resultsDirectory": "perf/results",
          "scenarios": [
            {
              "name": "upload",
              "script": "scenarios/smoke.js",
              "baseUrlVariable": "APP_UPLOAD_URL",
              "host": {
                "command": "app-host",
                "arguments": ["--urls", "{listenUrl}"],
                "readyFileVariable": "APP_READY_FILE",
                "healthPath": "/health",
                "readyTimeoutSeconds": 10,
                "healthTimeoutSeconds": {{healthTimeoutSeconds.ToString(CultureInfo.InvariantCulture)}}
              }
            }
          ]
        }
        """;

    /// <summary>Creates settings for <paramref name="scenario"/> with the given environment.</summary>
    public LoadTestSettings Settings(string scenario = "smoke", params (string Name, string Value)[] environment) => new()
    {
        Root = Root,
        ConfigurationPath = ConfigurationPath,
        Scenario = scenario,
        Environment = environment.ToDictionary(static variable => variable.Name, static variable => variable.Value, StringComparer.Ordinal),
    };

    /// <summary>Adds a fake executable and maps its command name to it.</summary>
    public RecordingFakeExecutable AddFake(string name, string body = "")
    {
        var fake = new RecordingFakeExecutable(Path.Combine(Root, "fakes"), name, 0, body);
        Executables.Set(name, fake.Path);
        return fake;
    }

    /// <summary>Lists the run directories.</summary>
    public string[] RunDirectories() =>
        Directory.Exists(ResultsDirectory) ? Directory.GetDirectories(ResultsDirectory) : [];

    /// <summary>Deletes the repository.</summary>
    public void Dispose()
    {
        Error.Dispose();
        _directory.Dispose();
    }
}
