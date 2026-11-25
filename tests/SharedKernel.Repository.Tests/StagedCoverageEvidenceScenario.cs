namespace PandaBear.Repository.Tests;

/// <summary>Exercises coverage capture and staged measurement through the CI command tree.</summary>
internal sealed class StagedCoverageEvidenceScenario : IDisposable
{
    /// <summary>The Git tree under measurement.</summary>
    public const string CurrentTree = "1111111111111111111111111111111111111111";

    /// <summary>A different tree with the same relative source filename.</summary>
    public const string PreviousTree = "2222222222222222222222222222222222222222";

    private const string ReportPath = "execution-library/results/Core.Tests/net11.0_x64/coverage.cobertura.1.xml";
    private const string CoreTests = "tests/Core.Tests/Core.Tests.csproj";
    private readonly CoverageTestScope _scope = new(0);
    private string _report = string.Empty;

    /// <summary>Creates the fixture and the log of actual CLI child-process requests.</summary>
    public StagedCoverageEvidenceScenario() => Processes = new ProcessLog(Respond);

    /// <summary>Gets the process requests of the tool.</summary>
    public ProcessLog Processes { get; }

    /// <summary>Gets or sets the current Git index tree.</summary>
    public string Tree { get; set; } = CurrentTree;

    /// <summary>Gets or sets the tree left behind after the test process exits.</summary>
    public string? TreeAfterTest { get; set; }

    /// <summary>Gets or sets whether Git reports tracked changes outside the index.</summary>
    public bool Unstaged { get; set; }

    /// <summary>Runs integrated capture and has the test process generate Cobertura from those line numbers.</summary>
    public Task<RepositoryToolResult> Capture(string directory, params int[] lines)
    {
        _report = CoverageTestScope.Report("src/Core.cs", lines.Select(static line => (line, true, (string?)null)));
        _scope.Repository.WriteSelectionIn(directory, CoverageTestScope.Revision, ("library", [CoreTests]));
        return Invoke("run", "--selection", directory, "--slice", "library", "--coverage", "--coverage-evidence");
    }

    /// <summary>Measures using the evidence produced by each supplied capture directory.</summary>
    public Task<RepositoryToolResult> Measure(params string[] directories) =>
        Invoke(["coverage", "measure-staged", .. directories.SelectMany(static directory => new[] { "--evidence", EvidencePath(directory) })]);

    /// <summary>Invokes the CI tool with the scenario's working tree and recorded processes.</summary>
    public Task<RepositoryToolResult> Invoke(params string[] arguments) =>
        RepositoryToolInvocation.Run(_scope.Root, arguments, Processes.Run, TestContext.Current.CancellationToken);

    /// <summary>Replaces the contents of an evidence file to exercise validation failures.</summary>
    public void ChangeEvidence(string directory, string content) => _scope.WriteFile(EvidencePath(directory), content);

    /// <summary>Writes different bytes to a collected report.</summary>
    public void ChangeReport(string directory) => _scope.WriteFile(directory + "/" + ReportPath, "<coverage />");

    /// <summary>Checks whether a capture emitted evidence.</summary>
    public bool HasEvidence(string directory) => File.Exists(Path.Combine(_scope.Root, EvidencePath(directory)));

    /// <inheritdoc />
    public void Dispose() => _scope.Dispose();

    private static string EvidencePath(string directory) => directory + "/execution-library/results/coverage-evidence.json";

    private Task<string> Respond(string fileName, string[] arguments, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (arguments is ["test", ..])
        {
            var results = arguments[Array.IndexOf(arguments, "--results-directory") + 1];
            _scope.WriteFile(Path.Combine(results, "Core.Tests/net11.0_x64/coverage.cobertura.1.xml"), _report);
            Tree = TreeAfterTest ?? Tree;
        }

        return Task.FromResult(fileName == "git" ? arguments switch
        {
            ["rev-parse", "HEAD"] => CoverageTestScope.Revision,
            ["write-tree"] => Tree,
            [.., "HEAD" or CoverageTestScope.BaseRevision, CurrentTree or PreviousTree, "--"] => _scope.Patch!,
            _ when arguments.Contains("--cached") => _scope.Patch!,
            ["diff", ..] when Unstaged => "src/Core.cs\0",
            _ => string.Empty,
        } : string.Empty);
    }
}
