using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace PandaBear.Repository.Tests;

/// <summary>
/// Creates a consumer root, a library root, a feed with two candidate packages, and a pin that names them, all under one
/// temporary directory.
/// </summary>
internal sealed class CandidateStagingScope : IDisposable
{
    public const string Revision = "0123456789abcdef0123456789abcdef01234567";
    public const string Version = "1.2.3-alpha.4.1";
    public const string Repository = "danigutsch/PandaBear.SharedKernel";
    public const string GlobalJson = """{ "sdk": { "version": "10.0.402", "rollForward": "disable" } }""";

    public CandidateStagingScope()
    {
        Base = Directory.CreateTempSubdirectory("sharedkernel-stage-").FullName;
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

    public string Base
    {
        get;
    }

    public string Root
    {
        get;
    }

    public string LibraryRoot
    {
        get;
    }

    public string Feed
    {
        get;
    }

    public string Output
    {
        get;
    }

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

    public void Dispose() => Directory.Delete(Base, recursive: true);

    private void AddPackage(string name)
    {
        var content = RandomNumberGenerator.GetBytes(4096);
        Packages[name] = content;
        File.WriteAllBytes(Path.Combine(Feed, name), content);
    }
}
