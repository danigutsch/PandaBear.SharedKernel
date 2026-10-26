using System.CommandLine;

namespace PandaBear.K6.Tool.Tests;

/// <summary>A temporary repository with a k6 configuration, fake executables, and a fake environment.</summary>
internal sealed class K6TestRepository : IDisposable
{
    public const string Image = "registry.example/k6:1.0@sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

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

    public K6TestRepository(string configuration = DefaultConfiguration)
    {
        Directory.CreateDirectory(Path.Combine(Root, ".config"));
        Directory.CreateDirectory(Path.Combine(Root, "perf", "k6", "scenarios"));
        File.WriteAllText(Path.Combine(Root, "perf", "k6", "scenarios", "smoke.js"), "export default function () {}\n");
        WriteConfiguration(configuration);
    }

    public string Root => _directory.Path;

    public string ResultsDirectory => Path.Combine(Root, "perf", "results");

    public StringWriter Output { get; } = new();

    public StringWriter Error { get; } = new();

    public void WriteConfiguration(string configuration) =>
        File.WriteAllText(Path.Combine(Root, ".config", "sharedkernel-k6.json"), configuration);

    public void SetEnvironment(string name, string? value) => _environment[name] = value;

    public void SetExecutable(string name, string path) => _executables[name] = path;

    /// <summary>Adds a POSIX shell script that records its arguments and environment and exits with a code.</summary>
    public FakeExecutable AddFake(string name, int exitCode = 0, string body = "")
    {
        var fake = new FakeExecutable(Path.Combine(Root, "fakes"), name, exitCode, body);
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

    public string[] RunDirectories() =>
        Directory.Exists(ResultsDirectory) ? Directory.GetDirectories(ResultsDirectory) : [];

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

    public RootCommand Create() =>
        K6CommandLine.Create(Root, _environment.GetValueOrDefault, name => _executables.GetValueOrDefault(name));

    public void Dispose() => _directory.Dispose();
}
