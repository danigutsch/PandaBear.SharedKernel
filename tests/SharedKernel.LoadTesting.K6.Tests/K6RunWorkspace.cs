using System.Diagnostics;

namespace PandaBear.LoadTesting.K6.Tests;

/// <summary>A temporary root with a run directory, and the prepared k6 runs that point into it.</summary>
internal sealed class K6RunWorkspace : IDisposable
{
    private readonly TemporaryDirectory _directory = new();

    /// <summary>Gets the root directory.</summary>
    public string Root => _directory.Path;

    /// <summary>Gets what the generator writes to its diagnostics.</summary>
    public StringWriter Error { get; } = new();

    /// <summary>Creates an answer that writes the summary into the mounted results directory and exits with <paramref name="exitCode"/>.</summary>
    public static Func<ProcessStartInfo, CancellationToken, ProcessOutput> WritesSummary(int exitCode) => (startInfo, _) =>
    {
        var mount = startInfo.ArgumentList.Single(static argument => argument.EndsWith(",target=/results", StringComparison.Ordinal));
        File.WriteAllText(Path.Combine(mount["type=bind,source=".Length..^",target=/results".Length], "k6-summary.json"), "{}");
        return new ProcessOutput(exitCode, string.Empty);
    };

    /// <summary>Creates a prepared run of the <c>smoke</c> script with a base URL, the usage report variable, and <c>--vus 2</c>.</summary>
    public LoadGeneratorRun Run(LoadTestContainer? container = null, string executable = "/usr/bin/k6", IReadOnlyDictionary<string, string>? environment = null)
    {
        var scripts = Path.Combine(Root, "perf", "k6");
        var runDirectory = Path.Combine(Root, "perf", "results", "smoke-run");
        Directory.CreateDirectory(runDirectory);
        return new LoadGeneratorRun
        {
            Executable = executable,
            Root = Root,
            ScriptDirectory = scripts,
            Script = "scenarios/smoke.js",
            ScriptPath = Path.Combine(scripts, "scenarios", "smoke.js"),
            RunDirectory = runDirectory,
            Variables = [new("APP_BASE_URL", "http://localhost:5000"), new("K6_NO_USAGE_REPORT", "true")],
            Arguments = ["--vus", "2"],
            Container = container,
            Environment = environment ?? new Dictionary<string, string>(StringComparer.Ordinal),
            Error = Error,
        };
    }

    /// <summary>Deletes the temporary directory.</summary>
    public void Dispose()
    {
        Error.Dispose();
        _directory.Dispose();
    }
}
