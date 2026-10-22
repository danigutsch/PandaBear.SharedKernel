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

    [Fact]
    public static void Candidate_directory_finds_one_nested_manifest_and_preserves_hash_verification()
    {
        // Arrange
        using var temporary = new TemporaryReleasePrepDirectory();
        var manifest = temporary.WriteCandidate();

        // Act
        var options = CandidateCommandOptions.Parse(["--candidate-directory", temporary.Root, "--aot"]);
        var evidence = CandidateManifest.ReadAndVerify(options.Candidate.ShouldNotBeNull());

        // Assert
        options.Candidate.ShouldBe(manifest);
        options.Aot.ShouldBeTrue();
        evidence["packageVersion"]!.GetValue<string>().ShouldBe("1.0.0");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public static void Candidate_directory_requires_exactly_one_manifest(int manifestCount)
    {
        // Arrange
        using var temporary = new TemporaryReleasePrepDirectory();
        if (manifestCount == 2)
        {
            var manifest = temporary.WriteCandidate();
            Directory.CreateDirectory(temporary.OutputDirectory);
            File.Copy(manifest, Path.Combine(temporary.OutputDirectory, "candidate.json"));
        }

        // Act
        Action parse = () => CandidateCommandOptions.Parse(["--candidate-directory", temporary.Root]);

        // Assert
        parse.ShouldThrow<ArgumentException>().Message.ShouldContain("exactly one candidate.json", StringComparison.Ordinal);
    }

    [Fact]
    public static void Candidate_directory_must_exist()
    {
        // Arrange
        using var temporary = new TemporaryReleasePrepDirectory();
        var missing = Path.Combine(temporary.Root, "missing");

        // Act
        Action parse = () => CandidateCommandOptions.Parse(["--candidate-directory", missing]);

        // Assert
        parse.ShouldThrow<ArgumentException>().Message.ShouldContain("directory", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void Candidate_directory_cannot_be_combined_with_an_explicit_manifest(bool directoryFirst)
    {
        // Arrange
        using var temporary = new TemporaryReleasePrepDirectory();
        var manifest = temporary.WriteCandidate();
        string[] options = directoryFirst
            ? ["--candidate-directory", temporary.Root, "--candidate", manifest]
            : ["--candidate", manifest, "--candidate-directory", temporary.Root];

        // Act
        Action parse = () => CandidateCommandOptions.Parse(options);

        // Assert
        parse.ShouldThrow<ArgumentException>().Message.ShouldContain("cannot be combined", StringComparison.Ordinal);
    }

    [Fact]
    public static async Task Consuming_a_discovered_manifest_rejects_tampered_packages_before_changing_evidence()
    {
        // Arrange
        using var temporary = new TemporaryReleasePrepDirectory();
        var manifest = temporary.WriteCandidate();
        var original = await File.ReadAllTextAsync(manifest, TestContext.Current.CancellationToken);
        await File.AppendAllTextAsync(Directory.GetFiles(temporary.PackageDirectory, "*.nupkg").ShouldHaveSingleItem(), "changed", TestContext.Current.CancellationToken);
        using var input = new StringReader(string.Empty);
        using var output = new StringWriter(System.Globalization.CultureInfo.InvariantCulture);
        using var error = new StringWriter(System.Globalization.CultureInfo.InvariantCulture);

        // Act
        var exitCode = await VersioningToolApplication.Run(
            ["consume-candidate", "--candidate-directory", temporary.Root], input, output, error);

        // Assert
        exitCode.ShouldBe(2);
        error.ToString().ShouldContain("Candidate package changed", StringComparison.Ordinal);
        var retained = await File.ReadAllTextAsync(manifest, TestContext.Current.CancellationToken);
        retained.ShouldBe(original);
    }

    [Fact]
    public static async Task Consume_help_documents_explicit_manifest_or_candidate_directory()
    {
        // Arrange
        using var input = new StringReader(string.Empty);
        using var output = new StringWriter(System.Globalization.CultureInfo.InvariantCulture);
        using var error = new StringWriter(System.Globalization.CultureInfo.InvariantCulture);

        // Act
        var exitCode = await VersioningToolApplication.Run(["consume-candidate", "--help"], input, output, error);

        // Assert
        exitCode.ShouldBe(0);
        output.ToString().ShouldContain("--candidate <path> | --candidate-directory <path>", StringComparison.Ordinal);
        error.ToString().ShouldBe(string.Empty);
    }

}
