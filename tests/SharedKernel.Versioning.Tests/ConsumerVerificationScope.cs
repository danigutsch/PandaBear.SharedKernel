using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using PandaBear.Tooling;
using PandaBear.Versioning.Tool;

namespace PandaBear.Versioning.Tests;

/// <summary>
/// A consumer repository that references the candidate version of one SharedKernel package and one SharedKernel tool,
/// and a candidate directory, outside it, with that package and its manifest.
/// </summary>
internal sealed partial class ConsumerVerificationScope : IDisposable
{
    /// <summary>
    /// The candidate package version used by the consumer repository and manifest.
    /// </summary>
    public const string Version = "1.0.0-alpha.1";

    /// <summary>
    /// File name of the candidate Results package.
    /// </summary>
    public const string Package = "PandaBear.SharedKernel.Results." + Version + ".nupkg";

    /// <summary>
    /// NuGet.Config content that maps PandaBear.SharedKernel.* packages to the local feed and everything else to nuget.org.
    /// </summary>
    public const string NuGetConfig = """
        <configuration>
          <packageSources>
            <clear />
            <add key="sharedkernel" value=".local/sharedkernel-feed" />
            <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
          </packageSources>
          <packageSourceMapping>
            <packageSource key="sharedkernel"><package pattern="PandaBear.SharedKernel.*" /></packageSource>
            <packageSource key="nuget.org"><package pattern="*" /></packageSource>
          </packageSourceMapping>
          <fallbackPackageFolders><clear /></fallbackPackageFolders>
        </configuration>
        """;

    private readonly TemporaryGitRepository repository = new();

    /// <summary>
    /// Writes the consumer repository files, the candidate package, and an in-memory candidate manifest that is not yet written to disk.
    /// </summary>
    public ConsumerVerificationScope()
    {
        WriteFile(".gitignore", "TestResults/\n.local/\n");
        WriteFile("Consumer.slnx", """<Solution><Folder Name="/src/"><Project Path="src/Consumer/Consumer.csproj" /></Folder></Solution>""");
        WriteFile("src/Consumer/Consumer.csproj", "<Project />");
        WriteFile("src/Consumer/packages.lock.json", Lock("Direct", Version));
        WriteFile("Directory.Packages.props", """<Project><PackageVersion Include="PandaBear.SharedKernel.Results" Version="1.0.0-alpha.1" /></Project>""");
        WriteFile(".config/dotnet-tools.json", """{ "version": 1, "isRoot": true, "tools": { "pandabear.sharedkernel.ci.tool": { "version": "1.0.0-alpha.1", "commands": ["sharedkernel-ci"] } } }""");
        WriteFile("NuGet.Config", NuGetConfig);
        Directory.CreateDirectory(CandidateDirectory);
        File.WriteAllText(Path.Combine(CandidateDirectory, Package), "candidate package bytes");
        Candidate = new JsonObject
        {
            ["libraryRevision"] = "0123456789abcdef0123456789abcdef01234567",
            ["libraryDirty"] = false,
            ["packageVersion"] = Version,
            ["packageSha256"] = new JsonObject { [Package] = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("candidate package bytes"))) },
            ["consumerCompatibility"] = "pending",
        };
    }

    /// <summary>
    /// Gets the root directory of the consumer repository.
    /// </summary>
    public string Root => repository.Root;

    /// <summary>
    /// Gets the temporary directory, outside the repository, that holds the candidate package and manifest.
    /// </summary>
    public string CandidateDirectory { get; } = Path.Combine(Path.GetTempPath(), "versioning-candidate-" + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// Gets the path of the candidate manifest file.
    /// </summary>
    public string Manifest => Path.Combine(CandidateDirectory, "candidate.json");

    /// <summary>
    /// Gets the path of the compatibility evidence file the verification writes.
    /// </summary>
    public string EvidencePath => Path.Combine(Root, "TestResults", "consumer-compatibility.json");

    /// <summary>
    /// Gets the candidate manifest content, which tests may change before it is committed.
    /// </summary>
    public JsonObject Candidate
    {
        get;
    }

    /// <summary>
    /// Gets the compatibility evidence parsed from the evidence file.
    /// </summary>
    public JsonObject Evidence => JsonNode.Parse(File.ReadAllText(EvidencePath))!.AsObject();

    /// <summary>A command that prints the clone directory, the staged packages, the NuGet cache variables, and HEAD.</summary>
    public static string[] Probe(string extra = "") =>
        ["git", "-c", "alias.probe=!pwd; ls .local/sharedkernel-feed; echo \"packages=$NUGET_PACKAGES\"; echo \"http=$NUGET_HTTP_CACHE_PATH\"; git rev-parse HEAD" + extra, "probe"];

    /// <summary>
    /// Returns a packages.lock.json for the Results package with the given dependency type and resolved version, plus an unrelated external package.
    /// </summary>
    public static string Lock(string type, string resolved) =>
        "{ \"version\": 2, \"dependencies\": { \"net10.0\": { \"PandaBear.SharedKernel.Results\": { \"type\": \"" + type + "\", \"resolved\": \"" + resolved
            + "\" }, \"External.Package\": { \"type\": \"Direct\", \"resolved\": \"2.0.0\" } } } }";

    /// <summary>Returns the scratch directory that a probe's <c>pwd</c> printed, under the temporary directory of this process.</summary>
    public static string ScratchDirectory(string output) => Path.Combine(Path.GetTempPath(), Scratch().Match(output).Value);

    /// <summary>
    /// Writes a file under the consumer repository root, creating missing directories.
    /// </summary>
    public void WriteFile(string path, string content)
    {
        var fullPath = Path.Combine(Root, path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
    }

    /// <summary>Writes the manifest, commits every file, and returns the commit.</summary>
    public async Task<string> Commit(CancellationToken ct)
    {
        CandidateManifest.Write(Manifest, Candidate);
        if (!Directory.Exists(Path.Combine(Root, ".git")))
        {
            await repository.Initialize(ct);
        }

        await CommandRunner.Run("git", ["add", "--all"], Root, ct);
        await CommandRunner.Run("git", ["commit", "--quiet", "--message", "test: consumer"], Root, ct);
        return (await CommandRunner.Run("git", ["rev-parse", "HEAD"], Root, ct)).Trim();
    }

    /// <summary>
    /// Runs the verify-consumer command for the candidate and repository with the given options and command.
    /// </summary>
    public Task<CommandLineInvocation> Verify(string[] command, CancellationToken ct, params string[] options) =>
        VersioningCommandLineRunner.Invoke(["verify-consumer", "--candidate", Manifest, "--repo-root", Root, .. options, "--", .. command], ct);

    /// <summary>
    /// Deletes the consumer repository and the candidate directory.
    /// </summary>
    public void Dispose()
    {
        repository.Dispose();
        Directory.Delete(CandidateDirectory, recursive: true);
    }

    [GeneratedRegex("sharedkernel-consumer-[0-9a-f]{32}", RegexOptions.CultureInvariant)]
    private static partial Regex Scratch();
}
