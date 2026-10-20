using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using PandaBear.Versioning.Tool;

namespace PandaBear.Versioning.Tests;

public static class CandidateManifestTests
{
    [Fact]
    public static async Task Downloaded_candidates_generate_configuration_for_the_current_feed_location()
    {
        using var temporary = new TemporaryReleasePrepDirectory();
        var package = NuGetPackageBuilder.WritePackage(temporary.PackageDirectory, "PandaBear.SharedKernel.Probe", "1.0.0");
        using var stream = File.OpenRead(package);
        var hashes = new JsonObject { [Path.GetFileName(package)] = Convert.ToHexStringLower((await SHA256.HashDataAsync(stream, TestContext.Current.CancellationToken).ConfigureAwait(true))) };
        var manifest = Path.Combine(temporary.PackageDirectory, "candidate.json");
        CandidateManifest.Write(manifest, new JsonObject { ["packageVersion"] = "1.0.0", ["packageSha256"] = hashes });
        CandidateManifest.ReadAndVerify(manifest)["packageVersion"]!.GetValue<string>().ShouldBe("1.0.0");
        Directory.CreateDirectory(temporary.OutputDirectory);
        var ids = SharedKernelLocalFeed.ReadPackageIds([package], "1.0.0");
        await SharedKernelLocalFeed.WriteNuGetConfig(temporary.OutputDirectory, temporary.PackageDirectory, ids, new Uri("https://example.test/v3/index.json"));
        var config = XDocument.Load(Path.Combine(temporary.OutputDirectory, "NuGet.config"));
        config.Descendants("add").ShouldContain(x => (string?)x.Attribute("value") == temporary.PackageDirectory);
        config.Descendants("package").ShouldContain(x => (string?)x.Attribute("pattern") == "PandaBear.SharedKernel.*");
    }

    [Fact]
    public static void Changed_candidate_packages_are_rejected()
    {
        using var temporary = new TemporaryReleasePrepDirectory();
        var package = NuGetPackageBuilder.WritePackage(temporary.PackageDirectory, "PandaBear.SharedKernel.Probe", "1.0.0");
        var manifest = Path.Combine(temporary.PackageDirectory, "candidate.json");
        CandidateManifest.Write(manifest, new JsonObject { ["packageVersion"] = "1.0.0", ["packageSha256"] = new JsonObject { [Path.GetFileName(package)] = "changed" } });
        Action check = () => CandidateManifest.ReadAndVerify(manifest);
        check.ShouldThrow<ArgumentException>().Message.ShouldContain("Candidate package changed", StringComparison.Ordinal);
    }

    [Fact]
    public static void Candidate_package_paths_cannot_escape_the_feed()
    {
        using var temporary = new TemporaryReleasePrepDirectory();
        var manifest = Path.Combine(temporary.PackageDirectory, "candidate.json");
        CandidateManifest.Write(manifest, new JsonObject { ["packageVersion"] = "1.0.0", ["packageSha256"] = new JsonObject { ["../outside.nupkg"] = "changed" } });
        Action check = () => CandidateManifest.ReadAndVerify(manifest);
        check.ShouldThrow<ArgumentException>().Message.ShouldContain("filename", StringComparison.Ordinal);
    }
}
