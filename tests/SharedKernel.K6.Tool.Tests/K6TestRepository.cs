using System.CommandLine;

namespace PandaBear.K6.Tool.Tests;

/// <summary>A temporary repository with a k6 configuration, fake executables, and a fake environment.</summary>
internal sealed class K6TestRepository : IDisposable
{
    /// <summary>A digest-pinned container image reference used in test configurations.</summary>
    public const string Image = "registry.example/k6:1.0@sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    /// <summary>A configuration with a <c>smoke</c> scenario and a podman container section.</summary>
    public const string DefaultConfiguration = $$"""
        {
          "version": 1,
          "scriptDirectory": "perf/k6",
          "resultsDirectory": "perf/results",
          "container": { "engine": "podman", "image": "{{Image}}" },
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
    private readonly Dictionary<string, string?> _environment = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _executables = new(StringComparer.Ordinal);

    /// <summary>Creates the repository layout with a smoke script and writes <c>configuration</c> as the configuration file.</summary>
    public K6TestRepository(string configuration = DefaultConfiguration)
    {
        Directory.CreateDirectory(Path.Combine(Root, ".config"));
        Directory.CreateDirectory(Path.Combine(Root, "perf", "k6", "scenarios"));
        File.WriteAllText(Path.Combine(Root, "perf", "k6", "scenarios", "smoke.js"), "export default function () {}\n");
        WriteConfiguration(configuration);
    }

    /// <summary>The root directory of the temporary repository.</summary>
    public string Root => _directory.Path;

    /// <summary>The directory the repository's runs write results into.</summary>
    public string ResultsDirectory => Path.Combine(Root, "perf", "results");

    /// <summary>Captures what the command line writes to standard output.</summary>
    public StringWriter Output { get; } = new();

    /// <summary>Captures what the command line writes to standard error.</summary>
    public StringWriter Error { get; } = new();

    /// <summary>Replaces the repository's k6 configuration file.</summary>
    public void WriteConfiguration(string configuration) =>
        File.WriteAllText(Path.Combine(Root, ".config", "sharedkernel-k6.json"), configuration);

    /// <summary>Sets, or with <c>null</c> removes, a variable in the fake environment the command line sees.</summary>
    public void SetEnvironment(string name, string? value) => _environment[name] = value;

    /// <summary>Maps an executable name to the path the command line resolves it to.</summary>
    public void SetExecutable(string name, string path) => _executables[name] = path;

    /// <summary>Adds a POSIX shell script that records its arguments and environment and exits with a code.</summary>
    public RecordingFakeExecutable AddFake(string name, int exitCode = 0, string body = "")
    {
        var fake = new RecordingFakeExecutable(Path.Combine(Root, "fakes"), name, exitCode, body);
        _executables[name] = fake.Path;
        return fake;
    }

    /// <summary>A fake container engine body that writes the summary file into the mounted results directory.</summary>
    public const string WritesContainerSummary = """
        for argument in "$@"; do
          case "$argument" in
            type=bind,source=*,target=/results)
              results="${argument#type=bind,source=}"
              : > "${results%,target=/results}/k6-summary.json"
              ;;
          esac
        done
        """;

    /// <summary>Lists the run directories under the results directory.</summary>
    public string[] RunDirectories() =>
        Directory.Exists(ResultsDirectory) ? Directory.GetDirectories(ResultsDirectory) : [];

    /// <summary>Parses and runs a command line against this repository and returns its exit code.</summary>
    public Task<int> Invoke(string[] args, CancellationToken ct)
    {
        var configuration = new InvocationConfiguration
        {
            Output = Output,
            Error = Error,
            // Signal handlers are process-wide, so the test host registers none.
            ProcessTerminationTimeout = null,
        };
        return Create().Parse(args).InvokeAsync(configuration, ct);
    }

    /// <summary>Creates the root command wired to this repository's root, environment, and executables.</summary>
    public RootCommand Create() =>
        K6CommandLine.Create(Root, _environment.GetValueOrDefault, name => _executables.GetValueOrDefault(name));

    /// <summary>Deletes the temporary repository.</summary>
    public void Dispose() => _directory.Dispose();
}
