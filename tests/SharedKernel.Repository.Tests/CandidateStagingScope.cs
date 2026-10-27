using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using PandaBear.Testing;

namespace PandaBear.Repository.Tests;

/// <summary>
/// Creates a consumer root, a library root, a feed with two candidate packages, and a pin that names them, all under one
/// temporary directory.
/// </summary>
internal sealed class CandidateStagingScope : IDisposable
{
    /// <summary>The 40-character library commit hash that the pin records as <c>libraryRevision</c>.</summary>
    public const string Revision = "0123456789abcdef0123456789abcdef01234567";

    /// <summary>The prerelease package version that the pin records and the feed packages carry in their file names.</summary>
    public const string Version = "1.2.3-alpha.4.1";

    /// <summary>The GitHub owner and repository name used when a test stages from a release instead of the feed.</summary>
    public const string Repository = "danigutsch/PandaBear.SharedKernel";

    /// <summary>The pinned global.json content written to both the consumer and library roots.</summary>
    public const string GlobalJson = """{ "sdk": { "version": "10.0.402", "rollForward": "disable" } }""";

    // Git creates its object files read-only, which Windows refuses to delete until the attribute is cleared.
    private readonly TemporaryDirectory directory = new();
    private readonly List<string> lockedDirectories = [];

    /// <summary>
    /// Creates the consumer and library roots, fills the feed with the Results and CI tool packages plus one unlisted
    /// package, and writes a pin that names only the two listed packages.
    /// </summary>
    public CandidateStagingScope()
    {
        Base = directory.Path;
        Root = Path.Combine(Base, "consumer");
        LibraryRoot = Path.Combine(Base, "library");
        Feed = Path.Combine(Base, "feed");
        Output = Path.Combine(Root, ".local", "sharedkernel-feed");
        PinPath = Path.Combine(Root, ".config", "sharedkernel-candidate.json");
        Directory.CreateDirectory(Path.GetDirectoryName(PinPath)!);
        Directory.CreateDirectory(LibraryRoot);
        Directory.CreateDirectory(Feed);
        File.WriteAllText(Path.Combine(Root, "global.json"), GlobalJson);
        File.WriteAllText(Path.Combine(LibraryRoot, "global.json"), GlobalJson);
        AddPackage("PandaBear.SharedKernel.Results." + Version + ".nupkg");
        AddPackage("PandaBear.SharedKernel.CI.Tool." + Version + ".nupkg");
        File.WriteAllText(Path.Combine(Feed, "PandaBear.SharedKernel.Unlisted." + Version + ".nupkg"), "not in the pin");
        Pin = WritePin(CreatePin());
    }

    /// <summary>Gets the temporary directory that contains the consumer root, the library root, and the feed.</summary>
    public string Base
    {
        get;
    }

    /// <summary>Gets the consumer repository root that holds the pin, the global.json, and the staged output.</summary>
    public string Root
    {
        get;
    }

    /// <summary>Gets the library repository root, which holds its own copy of the global.json.</summary>
    public string LibraryRoot
    {
        get;
    }

    /// <summary>Gets the directory that holds the candidate packages the staging command copies from.</summary>
    public string Feed
    {
        get;
    }

    /// <summary>Gets the consumer's <c>.local/sharedkernel-feed</c> directory, where the staged packages must end up.</summary>
    public string Output
    {
        get;
    }

    /// <summary>Gets the path of the <c>sharedkernel-candidate.json</c> pin file under the consumer's .config directory.</summary>
    public string PinPath
    {
        get;
    }

    /// <summary>Gets the exact bytes of the pin file.</summary>
    public byte[] Pin
    {
        get; private set;
    }

    /// <summary>Gets the package contents by file name.</summary>
    public Dictionary<string, byte[]> Packages { get; } = new(StringComparer.Ordinal);

    /// <summary>Gets the names of the feed packages that the pin lists, sorted by ordinal comparison.</summary>
    public string[] PackageNames => [.. Packages.Keys.Order(StringComparer.Ordinal)];

    /// <summary>Returns a pin with the identity of the packages in the feed and some consumption evidence.</summary>
    public JsonObject CreatePin()
    {
        var hashes = new JsonObject();
        foreach (var name in PackageNames)
        {
            hashes[name] = Convert.ToHexStringLower(SHA256.HashData(Packages[name]));
        }

        return new JsonObject
        {
            ["libraryRevision"] = Revision,
            ["libraryDirty"] = false,
            ["packageVersion"] = Version,
            ["packageSha256"] = hashes,
            ["consumerCompatibility"] = "pending",
            ["linuxPackageConsumption"] = "passed",
        };
    }

    /// <summary>Writes the pin indented, as create-candidate does, and returns its bytes.</summary>
    public byte[] WritePin(JsonObject pin) => WritePin(pin.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }) + "\n");

    /// <summary>Writes <paramref name="text"/> as UTF-8 to the pin file verbatim, so a test can store malformed or hand-edited content.</summary>
    public byte[] WritePin(string text)
    {
        Pin = Encoding.UTF8.GetBytes(text);
        File.WriteAllBytes(PinPath, Pin);
        return Pin;
    }

    /// <summary>Returns the stage-candidate arguments for the feed or, with <paramref name="repository"/>, a release.</summary>
    public string[] Arguments(string? repository = null) =>
    [
        "stage-candidate",
        "--pin", PinPath,
        "--library-root", LibraryRoot,
        "--output", Output,
        .. repository is null ? new[] { "--feed", Feed } : ["--repository", repository],
        "--root", Root,
    ];

    /// <summary>Returns the staging directories that a run left beside the output.</summary>
    public string[] ScratchDirectories() =>
        Directory.Exists(Path.GetDirectoryName(Output))
            ? Directory.GetDirectories(Path.GetDirectoryName(Output)!, ".sharedkernel-feed.stage-*")
            : [];

    /// <summary>
    /// Creates a staging directory, two days old, that this process cannot delete: it holds a read-only file on Windows
    /// and, elsewhere, a file in a directory without write permission. Disposal unlocks it.
    /// </summary>
    public string AddUndeletableStaleScratch()
    {
        var scratch = Directory.CreateDirectory(Path.Combine(Path.GetDirectoryName(Output)!, ".sharedkernel-feed.stage-" + Guid.NewGuid().ToString("N"))).FullName;
        var locked = Directory.CreateDirectory(Path.Combine(scratch, "locked")).FullName;
        var file = Path.Combine(locked, "partial.nupkg");
        File.WriteAllText(file, "partial");
        if (OperatingSystem.IsWindows())
        {
            File.SetAttributes(file, FileAttributes.ReadOnly);
        }
        else
        {
            File.SetUnixFileMode(locked, UnixFileMode.UserRead | UnixFileMode.UserExecute);
            lockedDirectories.Add(locked);
        }

        Directory.SetLastWriteTimeUtc(scratch, TimeProvider.System.GetUtcNow().UtcDateTime.AddDays(-2));
        return scratch;
    }

    /// <summary>Restores write permission on directories that a test locked, then deletes the temporary directory.</summary>
    public void Dispose()
    {
        foreach (var locked in lockedDirectories)
        {
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(locked, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
        }

        directory.Dispose();
    }

    private void AddPackage(string name)
    {
        var content = RandomNumberGenerator.GetBytes(4096);
        Packages[name] = content;
        File.WriteAllBytes(Path.Combine(Feed, name), content);
    }
}
