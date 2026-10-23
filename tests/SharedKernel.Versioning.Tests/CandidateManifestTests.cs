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
        // Arrange
        using var temporary = new TemporaryReleasePrepDirectory();
        var package = NuGetPackageBuilder.WritePackage(temporary.PackageDirectory, "PandaBear.SharedKernel.Probe", "1.0.0");
        using var stream = File.OpenRead(package);
        var hashes = new JsonObject { [Path.GetFileName(package)] = Convert.ToHexStringLower((await SHA256.HashDataAsync(stream, TestContext.Current.CancellationToken).ConfigureAwait(true))) };
        var manifest = Path.Combine(temporary.PackageDirectory, "candidate.json");
        CandidateManifest.Write(manifest, new JsonObject { ["packageVersion"] = "1.0.0", ["packageSha256"] = hashes });

        // Act
        var packageVersion = CandidateManifest.ReadAndVerify(manifest)["packageVersion"]!.GetValue<string>();
        Directory.CreateDirectory(temporary.OutputDirectory);
        var ids = SharedKernelLocalFeed.ReadPackageIds([package], "1.0.0");
        await SharedKernelLocalFeed.WriteNuGetConfig(temporary.OutputDirectory, temporary.PackageDirectory, ids, new Uri("https://example.test/v3/index.json"));
        var config = XDocument.Load(Path.Combine(temporary.OutputDirectory, "NuGet.config"));

        // Assert
        packageVersion.ShouldBe("1.0.0");
        config.Descendants("add").ShouldContain(x => (string?)x.Attribute("value") == temporary.PackageDirectory);
        config.Descendants("package").ShouldContain(x => (string?)x.Attribute("pattern") == "PandaBear.SharedKernel.*");
    }

    [Fact]
    public static void Changed_candidate_packages_are_rejected()
    {
        // Arrange
        using var temporary = new TemporaryReleasePrepDirectory();
        var package = NuGetPackageBuilder.WritePackage(temporary.PackageDirectory, "PandaBear.SharedKernel.Probe", "1.0.0");
        var manifest = Path.Combine(temporary.PackageDirectory, "candidate.json");
        CandidateManifest.Write(manifest, new JsonObject { ["packageVersion"] = "1.0.0", ["packageSha256"] = new JsonObject { [Path.GetFileName(package)] = "changed" } });
        Action check = () => CandidateManifest.ReadAndVerify(manifest);

        // Act
        var exception = check.ShouldThrow<ArgumentException>();

        // Assert
        exception.Message.ShouldContain("Candidate package changed", StringComparison.Ordinal);
    }

    [Fact]
    public static void Candidate_package_paths_cannot_escape_the_feed()
    {
        // Arrange
        using var temporary = new TemporaryReleasePrepDirectory();
        var manifest = Path.Combine(temporary.PackageDirectory, "candidate.json");
        CandidateManifest.Write(manifest, new JsonObject { ["packageVersion"] = "1.0.0", ["packageSha256"] = new JsonObject { ["../outside.nupkg"] = "changed" } });
        Action check = () => CandidateManifest.ReadAndVerify(manifest);

        // Act
        var exception = check.ShouldThrow<ArgumentException>();

        // Assert
        exception.Message.ShouldContain("filename", StringComparison.Ordinal);
    }

    [Fact]
    public static void Candidate_directory_finds_one_nested_manifest_and_preserves_hash_verification()
    {
        // Arrange
        using var temporary = new TemporaryReleasePrepDirectory();
        var manifest = temporary.WriteCandidate();

        // Act
        var found = CandidateManifest.Find(temporary.Root);
        var evidence = CandidateManifest.ReadAndVerify(found);

        // Assert
        found.ShouldBe(manifest);
        evidence["packageVersion"]!.GetValue<string>().ShouldBe("1.0.0");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public static async Task Candidate_directory_requires_exactly_one_manifest(int manifestCount)
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
        var result = await VersioningCommandLineRunner.Invoke(
            ["consume-candidate", "--candidate-directory", temporary.Root],
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain("exactly one candidate.json", StringComparison.Ordinal);
    }

    [Fact]
    public static async Task Candidate_directory_must_exist()
    {
        // Arrange
        using var temporary = new TemporaryReleasePrepDirectory();
        var missing = Path.Combine(temporary.Root, "missing");

        // Act
        var result = await VersioningCommandLineRunner.Invoke(
            ["consume-candidate", "--candidate-directory", missing],
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain("Candidate directory does not exist: " + missing, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static async Task Candidate_directory_cannot_be_combined_with_an_explicit_manifest(bool directoryFirst)
    {
        // Arrange
        using var temporary = new TemporaryReleasePrepDirectory();
        var manifest = temporary.WriteCandidate();
        string[] options = directoryFirst
            ? ["--candidate-directory", temporary.Root, "--candidate", manifest]
            : ["--candidate", manifest, "--candidate-directory", temporary.Root];

        // Act
        var result = await VersioningCommandLineRunner.Invoke(["consume-candidate", .. options], TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.Error.ShouldContain("cannot be combined", StringComparison.Ordinal);
    }

    [Fact]
    public static async Task Consuming_a_discovered_manifest_rejects_tampered_packages_before_changing_evidence()
    {
        // Arrange
        using var temporary = new TemporaryReleasePrepDirectory();
        var manifest = temporary.WriteCandidate();
        var original = await File.ReadAllTextAsync(manifest, TestContext.Current.CancellationToken);
        await File.AppendAllTextAsync(Directory.GetFiles(temporary.PackageDirectory, "*.nupkg").ShouldHaveSingleItem(), "changed", TestContext.Current.CancellationToken);

        // Act
        var result = await VersioningCommandLineRunner.Invoke(
            ["consume-candidate", "--candidate-directory", temporary.Root],
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain("Candidate package changed", StringComparison.Ordinal);
        var retained = await File.ReadAllTextAsync(manifest, TestContext.Current.CancellationToken);
        retained.ShouldBe(original);
    }

    [Fact]
    public static async Task Canceled_consumption_leaves_recorded_evidence_unchanged()
    {
        // Arrange
        using var temporary = new TemporaryReleasePrepDirectory();
        var manifest = temporary.WriteCandidate();
        var evidence = CandidateManifest.ReadAndVerify(manifest);
        evidence["linuxPackageConsumption"] = "passed";
        evidence["windowsPackageConsumption"] = "passed";
        CandidateManifest.Write(manifest, evidence);
        var original = await File.ReadAllTextAsync(manifest, TestContext.Current.CancellationToken);
        await temporary.WriteConsumptionSamples("Canceled sample", TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        // Act
        var result = await VersioningCommandLineRunner.Invoke(
            ["consume-candidate", "--candidate", manifest, "--repo-root", temporary.Root],
            cancellation.Token);

        // Assert
        result.ExitCode.ShouldBe(1);
        var retained = await File.ReadAllTextAsync(manifest, TestContext.Current.CancellationToken);
        retained.ShouldBe(original);
        Directory.GetFiles(temporary.PackageDirectory, "candidate.json*").ShouldBe([manifest]);
    }

    [Fact]
    public static async Task Failed_consumption_records_failed_evidence()
    {
        // Arrange
        using var temporary = new TemporaryReleasePrepDirectory();
        var manifest = temporary.WriteCandidate();
        var platform = OperatingSystem.IsWindows() ? "windowsPackageConsumption" : "linuxPackageConsumption";

        // Act
        var result = await VersioningCommandLineRunner.Invoke(
            ["consume-candidate", "--candidate", manifest, "--repo-root", temporary.Root],
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(2);
        var evidence = CandidateManifest.ReadAndVerify(manifest);
        evidence[platform].ShouldNotBeNull().GetValue<string>().ShouldBe("failed");
        Directory.GetFiles(temporary.PackageDirectory, "candidate.json*").ShouldBe([manifest]);
    }
}
