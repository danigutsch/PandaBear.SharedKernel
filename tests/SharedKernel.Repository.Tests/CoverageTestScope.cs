using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace PandaBear.Repository.Tests;

/// <summary>
/// A repository with a coverage policy, a changed <c>src/Core.cs</c> of five added lines, its Cobertura report, and a
/// Release symbol file. Git answers come from <see cref="Patch"/> unless a test runs real Git.
/// </summary>
internal sealed class CoverageTestScope : IDisposable
{
    public const string BaseRevision = "0123456789abcdef0123456789abcdef01234567";

    public const string Revision = "89abcdef0123456789abcdef0123456789abcdef";

    public const string Parents = "1111111111111111111111111111111111111111\n2222222222222222222222222222222222222222\n";

    public CoverageTestScope(int coveredLines, string? conditions = null)
    {
        Repository.WriteFile(".config/sharedkernel-ci.json", Configuration(80));
        WriteFile("src/Core.cs", "one\ntwo\nthree\nfour\nfive\n");
        Patch = "--- /dev/null\n+++ b/src/Core.cs\n@@ -0,0 +1,5 @@\n+one\n+two\n+three\n+four\n+five\n";
        WriteFile("coverage.xml", Report("src/Core.cs", Enumerable.Range(1, 5).Select(number => (number, number <= coveredLines, number == 1 ? conditions : null))));

        // Recording requires a Release symbol file; this one is not a portable PDB, so it never exempts a file.
        WriteFile("src/Placeholder/bin/Release/net10.0/Placeholder.pdb", "symbols");
    }

    public RepositoryTestScope Repository { get; } = new();

    public string Root => Repository.Root;

    /// <summary>Gets or sets the output of <c>git diff</c>; <see langword="null"/> makes it fail.</summary>
    public string? Patch
    {
        get; set;
    }

    /// <summary>Gets a new log that answers <c>rev-parse HEAD</c>, <c>rev-parse HEAD^@</c>, and <c>diff</c>.</summary>
    public ProcessLog Processes => new(Answer);

    /// <summary>Returns the default CI configuration with a coverage policy of <c>src/*.cs</c> at <paramref name="threshold"/> percent.</summary>
    public static string Configuration(int threshold) => RepositoryTestScope.DefaultConfiguration.Replace(
        "\"version\": 1,",
        "\"version\": 1,\n  \"coverage\": { \"thresholdPercent\": " + threshold
            + ", \"include\": [\"src/*.cs\"], \"exclude\": [\"*.g.cs\", \"*/obj/*\"], \"symbolDirectories\": [\"src\"] },",
        StringComparison.Ordinal);

    /// <summary>Returns a Cobertura report of one class whose lines are hit or not, with optional condition coverage.</summary>
    public static string Report(string filename, IEnumerable<(int Number, bool Hit, string? Conditions)> lines) =>
        new XDocument(new XElement("coverage", new XElement("packages", new XElement("package", new XElement("classes", new XElement("class",
            new XAttribute("filename", filename),
            new XElement("lines", lines.Select(static line =>
            {
                var element = new XElement("line", new XAttribute("number", line.Number), new XAttribute("hits", line.Hit ? 1 : 0));
                if (line.Conditions is not null)
                {
                    element.Add(new XAttribute("branch", "true"), new XAttribute("condition-coverage", line.Conditions));
                }

                return element;
            })))))))).ToString();

    private Task<string> Answer(string fileName, string[] arguments, CancellationToken ct) => arguments switch
    {
        ["rev-parse", "HEAD"] => Task.FromResult(Revision + "\n"),
        ["rev-parse", "HEAD^@"] => Task.FromResult(Parents),
        _ when !arguments.Contains("diff") => Task.FromResult(string.Empty),
        _ when Patch is null => Task.FromException<string>(new InvalidOperationException("Command failed (128): git diff")),
        _ => Task.FromResult(Patch),
    };

    public void WriteFile(string path, string content) => Repository.WriteFile(path, content);

    public string ReadFile(string path) => File.ReadAllText(Path.Combine(Root, path));

    /// <summary>Records the symbols, then verifies <c>coverage.xml</c> against <see cref="BaseRevision"/>, both with <see cref="Processes"/>.</summary>
    public async Task<RepositoryToolResult> RecordAndVerify(params string[] reports)
    {
        var processes = Processes;
        var recorded = await RepositoryToolInvocation.Run(Root, ["coverage", "record-symbols"], processes.Run, TestContext.Current.CancellationToken);
        recorded.ExitCode.ShouldBe(0, recorded.Error);
        return await Verify(processes, reports);
    }

    public Task<RepositoryToolResult> Verify(ProcessLog processes, params string[] reports) =>
        RepositoryToolInvocation.Run(
            Root,
            ["coverage", "verify", .. (reports.Length == 0 ? ["coverage.xml"] : reports).SelectMany(static report => new[] { "--report", report }),
                "--base", BaseRevision, "--symbols", "TestResults/symbol-evidence.json"],
            processes.Run,
            TestContext.Current.CancellationToken);

    /// <summary>
    /// Writes <c>src/Core.cs</c> as one interface, its change, an empty report, and a portable PDB at
    /// <c>src/bin/Release/net10.0/Core.pdb</c> that names the file as the scenario requires.
    /// </summary>
    public void WriteSymbols(string scenario)
    {
        const string source = "public interface IValue { int Value { get; } }\n";
        WriteFile("src/Core.cs", source);
        WriteFile("coverage.xml", "<coverage><packages /></coverage>");
        Patch = "--- /dev/null\n+++ b/src/Core.cs\n@@ -0,0 +1 @@\n+" + source;
        var metadata = new MetadataBuilder();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(scenario == "stale-symbols" ? "old" : source));
        var name = scenario == "absolute-symbols" ? Path.Combine(Root, "src/Core.cs") : "/_/src/Core.cs";
        var algorithm = scenario == "unsupported-hash" ? Guid.Empty : new Guid("8829d00f-11b8-4213-878b-770e8597ac16");
        var document = metadata.AddDocument(metadata.GetOrAddDocumentName(name), metadata.GetOrAddGuid(algorithm), metadata.GetOrAddBlob(hash), default);
        var sequence = new BlobBuilder();
        if (scenario == "executable-symbols")
        {
            sequence.WriteBytes(new byte[] { 0, 0, 0, 1, 1, 1 });
        }

        metadata.AddMethodDebugInformation(document, metadata.GetOrAddBlob(sequence));
        var counts = new int[64];
        counts[(int)TableIndex.MethodDef] = 1;
        var blob = new BlobBuilder();
        new PortablePdbBuilder(metadata, ImmutableArray.Create(counts), default).Serialize(blob);
        WriteFile("src/bin/Release/net10.0/Core.pdb", string.Empty);
        File.WriteAllBytes(Path.Combine(Root, "src/bin/Release/net10.0/Core.pdb"), blob.ToArray());
    }

    public void Dispose() => Repository.Dispose();
}
