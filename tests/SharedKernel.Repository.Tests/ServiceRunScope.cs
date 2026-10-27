using System.Runtime.Versioning;

namespace PandaBear.Repository.Tests;

/// <summary>
/// A repository whose <c>database</c> slice runs a service that is a fake host, and a validation plan that selects the slice
/// and the <c>library</c> slice.
/// </summary>
[UnsupportedOSPlatform("windows")]
internal sealed class ServiceRunScope : IDisposable
{
    /// <summary>The test project of the <c>library</c> slice, which has no service.</summary>
    public const string CoreTests = "tests/Core.Tests/Core.Tests.csproj";

    /// <summary>The test project of the <c>database</c> slice, which receives the value that the service hands over.</summary>
    public const string DatabaseTests = "tests/Database.Tests/Database.Tests.csproj";

    /// <summary>The project that the <c>database</c> slice starts as its service.</summary>
    public const string DatabaseHost = "tests/Database.Host/Database.Host.csproj";

    /// <summary>The value that the fake host hands over. The tests look for it everywhere it must not appear.</summary>
    public const string Value = "Host=127.0.0.1;Port=5432;Username=postgres;Password=canary-9f2c1b";

    private readonly RepositoryTestScope repository = new();

    /// <param name="hostBody">The body of the fake host's script.</param>
    /// <param name="readyTimeoutSeconds">The configured time the service has to write its value.</param>
    /// <param name="stopTimeoutSeconds">The configured time the service has to exit after its input closes.</param>
    public ServiceRunScope(string hostBody, int readyTimeoutSeconds = 20, int stopTimeoutSeconds = 20)
    {
        Host = new FakeServiceHost(Path.Combine(repository.Root, "fake-service"));
        Host.Add(DatabaseHost, hostBody);
        Processes = new ProcessLog((fileName, arguments, ct) => Respond(fileName, arguments, ct));
        repository.AddTestProject(DatabaseTests, "src/Core/Core.csproj");
        repository.AddProject(DatabaseHost, "<Project />");
        repository.Configure($$"""
            [
              { "name": "library", "projects": [] },
              { "name": "database", "projects": ["{{DatabaseTests}}"], "service": { "project": "{{DatabaseHost}}", "readyFileVariable": "FAKE_READY_FILE", "valueVariable": "FAKE_SERVICE_VALUE", "readyTimeoutSeconds": {{readyTimeoutSeconds}}, "stopTimeoutSeconds": {{stopTimeoutSeconds}} } }
            ]
            """);
        Selection = repository.WriteSelection("fixture", ("library", [CoreTests]), ("database", [DatabaseTests]));
    }

    /// <summary>Gets the temporary repository that holds the projects, the configuration, and the plan.</summary>
    public RepositoryTestScope Repository => repository;

    /// <summary>Gets the fake host that stands in for the service process.</summary>
    public FakeServiceHost Host
    {
        get;
    }

    /// <summary>Gets the log that records the Git and dotnet requests of a run and answers them through <see cref="Respond"/>.</summary>
    public ProcessLog Processes
    {
        get;
    }

    /// <summary>Gets the plan directory.</summary>
    public string Selection
    {
        get;
    }

    /// <summary>Gets the execution directory of <c>run --slice database</c>.</summary>
    public string Execution => Path.Combine(Selection, "execution-database");

    /// <summary>Gets or sets how the fake <c>git</c> and <c>dotnet</c> answer; by default <c>git rev-parse</c> says <c>fixture</c> and everything else is silent.</summary>
    public Func<string, string[], CancellationToken, Task<string>> Respond
    {
        get; set;
    } =
        static (fileName, arguments, _) => Task.FromResult(fileName == "git" && arguments is ["rev-parse", ..] ? "fixture" : string.Empty);

    /// <summary>Gets the file that the fake host was told to write its value to.</summary>
    public string ReadyFile => Host.Environment(DatabaseHost)["FAKE_READY_FILE"];

    /// <summary>Runs the CI tool with <paramref name="args"/> in the scope's repository, recording processes in <see cref="Processes"/> and starting services through <see cref="Host"/>.</summary>
    public Task<RepositoryToolResult> Run(string[] args, CancellationToken ct) => RepositoryToolInvocation.Run(repository.Root, args, Processes, Host.Start, ct);

    /// <summary>Returns <c>&lt;phase&gt; &lt;status&gt;</c> for each line of the phase timings of <paramref name="execution"/>, the total line included.</summary>
    public static async Task<string[]> Phases(string execution, CancellationToken ct) =>
        [.. (await File.ReadAllLinesAsync(Path.Combine(execution, "phase-timings.tsv"), ct)).Skip(1).Select(static line => line.Split('\t')).Select(static fields => fields[0] + " " + fields[3])];

    /// <summary>Returns the files under the plan directory that contain <paramref name="secret"/>, relative to it.</summary>
    public async Task<string[]> FilesWith(string secret, CancellationToken ct)
    {
        List<string> found = [];
        foreach (var file in Directory.EnumerateFiles(Selection, "*", SearchOption.AllDirectories))
        {
            if ((await File.ReadAllTextAsync(file, ct)).Contains(secret, StringComparison.Ordinal))
            {
                found.Add(Path.GetRelativePath(Selection, file));
            }
        }

        return [.. found];
    }

    /// <summary>Kills the fake host if a test left it running, then deletes the repository.</summary>
    public void Dispose()
    {
        // A failed test must not leave its fake host running.
        Host.Kill(DatabaseHost);
        repository.Dispose();
    }
}
