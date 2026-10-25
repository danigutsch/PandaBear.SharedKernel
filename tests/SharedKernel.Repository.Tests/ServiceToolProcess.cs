using System.Runtime.Versioning;

namespace PandaBear.Repository.Tests;

/// <summary>
/// A <see cref="ServiceRunScope"/> whose tool runs in a process of its own, where signals and the default invocation
/// settings are real. The process finds <c>git</c> and <c>dotnet</c> as POSIX shell scripts first on its <c>PATH</c>:
/// <c>git</c> answers every request with the fixture revision, <c>dotnet run</c> becomes the fake host, <c>dotnet test</c>
/// runs the test body, and every other <c>dotnet</c> command succeeds at once. Scripts leave marker files in the directory
/// that their <c>FAKE_MARKERS</c> variable names, and the temporary directory is private to the scope.
/// </summary>
[UnsupportedOSPlatform("windows")]
internal sealed class ServiceToolProcess : IDisposable
{
    private readonly ServiceRunScope run;
    private readonly string bin;
    private readonly string markers;

    /// <param name="hostBody">The body of the fake host's script; it may write markers.</param>
    /// <param name="testBody">The body that stands in for <c>dotnet test</c>, after it wrote the <c>test.started</c> marker.</param>
    /// <param name="stopTimeoutSeconds">The configured time the fake host has to exit after its input closes.</param>
    public ServiceToolProcess(string hostBody, string testBody, int stopTimeoutSeconds = 30)
    {
        run = new ServiceRunScope(hostBody, stopTimeoutSeconds: stopTimeoutSeconds);
        var root = run.Repository.Root;
        bin = Path.Combine(root, "fake-bin");
        markers = Path.Combine(root, "markers");
        Temp = Path.Combine(root, "tmp");
        Directory.CreateDirectory(markers);
        Directory.CreateDirectory(Temp);
        FakeExecutable.WriteScript(Path.Combine(bin, "git"), "echo fixture\n");
        FakeExecutable.WriteScript(
            Path.Combine(bin, "dotnet"),
            $"""
            case "$1" in
            run) exec {FakeServiceHost.Quote(run.Host.ScriptPath(ServiceRunScope.DatabaseHost))} ;;
            test)
            : > "$FAKE_MARKERS/test.started"
            {testBody}
            ;;
            esac
            """ + "\n");
    }

    /// <summary>Gets the underlying scope, which records the fake host's environment.</summary>
    public ServiceRunScope Run => run;

    /// <summary>Gets the temporary directory of the tool process, where it creates the directory of the ready file.</summary>
    public string Temp
    {
        get;
    }

    /// <summary>Gets the execution directory of <c>run --slice database</c>.</summary>
    public string Execution => run.Execution;

    /// <summary>Gets the temporary directories that the tool created for a ready file and has not deleted.</summary>
    public string[] ServiceDirectories => Directory.GetDirectories(Temp, "sharedkernel-ci-service-*");

    /// <summary>Starts <c>run --slice database</c> in a process with <paramref name="environment"/> added to its environment.</summary>
    public RunningCiTool Start(IReadOnlyDictionary<string, string?>? environment = null)
    {
        var tool = new Dictionary<string, string?>
        {
            ["PATH"] = bin + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH"),
            ["TMPDIR"] = Temp,
            ["FAKE_MARKERS"] = markers,
        };
        foreach (var (name, value) in environment ?? new Dictionary<string, string?>())
        {
            tool[name] = value;
        }

        return CiToolProcess.Start(run.Repository.Root, ["run", "--slice", "database"], tool);
    }

    /// <summary>Reports whether a script wrote the marker <paramref name="name"/>.</summary>
    public bool HasMarker(string name) => File.Exists(Path.Combine(markers, name));

    /// <summary>Waits until a script wrote the marker <paramref name="name"/>, or fails when <paramref name="tool"/> exits first.</summary>
    public async Task WaitForMarker(string name, RunningCiTool tool, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        while (!HasMarker(name))
        {
            tool.HasExited.ShouldBeFalse("The tool exited before " + name + " appeared.");
            await Task.Delay(TimeSpan.FromMilliseconds(20), timeout.Token);
        }
    }

    public void Dispose() => run.Dispose();
}
