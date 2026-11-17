using System.Xml.Linq;

namespace PandaBear.Versioning.Tests;

/// <summary>
/// Tests the <c>--framework</c> option of the commands that build packed consumers, and the per-framework consumer runs and evidence.
/// </summary>
[Trait(SharedKernelTestTraitNames.CapabilityName, TestTraits.VersioningCapability)]
public static class ConsumerFrameworkTests
{
    [Theory]
    [InlineData("validate-candidate")]
    [InlineData("consume-candidate", "--candidate-directory", "artifacts/candidate")]
    [InlineData("consume-packages", "--feed", "packages", "--version", "1.2.3")]
    public static void Without_framework_the_option_is_empty(params string[] args)
    {
        // Act
        var parseResult = VersioningCommandLineRunner.Parse(args);

        // Assert
        parseResult.Errors.ShouldBeEmpty();
        var frameworks = parseResult.GetValue<string[]>("--framework") ?? [];
        frameworks.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("validate-candidate")]
    [InlineData("consume-candidate", "--candidate-directory", "artifacts/candidate")]
    [InlineData("consume-packages", "--feed", "packages", "--version", "1.2.3")]
    public static void Repeated_framework_options_bind_in_order(params string[] args)
    {
        // Act
        var parseResult = VersioningCommandLineRunner.Parse([.. args, "--framework", "net11.0", "--framework", "net10.0"]);

        // Assert
        parseResult.Errors.ShouldBeEmpty();
        parseResult.GetValue<string[]>("--framework").ShouldBe(["net11.0", "net10.0"]);
    }

    [Theory]
    [InlineData("net10", "validate-candidate")]
    [InlineData("NET10.0", "validate-candidate")]
    [InlineData("netcoreapp3.1", "consume-candidate", "--candidate-directory", "c")]
    [InlineData("net10.0-windows", "consume-packages", "--feed", "p", "--version", "1.2.3")]
    [InlineData("net10.0;net11.0", "validate-candidate")]
    [InlineData("", "validate-candidate")]
    public static async Task Invalid_frameworks_are_parse_errors(string framework, params string[] args)
    {
        // Act
        var result = await VersioningCommandLineRunner.Invoke([.. args, "--framework", framework], TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.Error.ShouldContain("Option '--framework' expects a target framework such as net10.0", StringComparison.Ordinal);
    }

    [Fact]
    public static async Task Duplicate_frameworks_are_a_parse_error()
    {
        // Act
        var result = await VersioningCommandLineRunner.Invoke(
            ["validate-candidate", "--framework", "net10.0", "--framework", "net10.0"], TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.Error.ShouldContain("Option '--framework' lists 'net10.0' more than once.", StringComparison.Ordinal);
    }

    [Fact]
    public static void Consumer_project_targets_the_framework()
    {
        // Arrange
        var project = XDocument.Parse("<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><OutputType>Exe</OutputType></PropertyGroup></Project>");

        // Act
        PackageConsumption.AddConsumerProperties(project, "net11.0");

        // Assert
        project.Descendants("TargetFramework").Select(static x => x.Value).ShouldBe(["net11.0"]);
    }

    [Fact]
    public static async Task Consumption_records_the_consumed_frameworks()
    {
        // Arrange
        using var temporary = new TemporaryReleasePrepDirectory();
        var manifest = temporary.WriteCandidate();
        await temporary.WriteConsumptionSamples("Consumed sample", TestContext.Current.CancellationToken);

        // Act
        var result = await VersioningCommandLineRunner.Invoke(
            ["consume-candidate", "--candidate", manifest, "--repo-root", temporary.Root, "--framework", "net10.0"],
            TestContext.Current.CancellationToken);
        using var evidence = new EvidenceWorkspaces(result);

        // Assert
        result.ExitCode.ShouldBe(0, result.Error);
        result.Error.ShouldContain("net10.0", StringComparison.Ordinal);
        var recorded = CandidateManifest.Load(manifest).Evidence;
        var platform = OperatingSystem.IsWindows() ? "windows" : "linux";
        recorded[platform + "PackageConsumptionFrameworks"]!.AsArray().Select(static x => x!.GetValue<string>()).ShouldBe(["net10.0"]);
    }

    [Fact]
    public static async Task Consumption_without_framework_uses_the_repository_framework()
    {
        // Arrange
        using var temporary = new TemporaryReleasePrepDirectory();
        var manifest = temporary.WriteCandidate();
        await temporary.WriteConsumptionSamples("Consumed sample", TestContext.Current.CancellationToken);

        // Act
        var result = await VersioningCommandLineRunner.Invoke(
            ["consume-candidate", "--candidate", manifest, "--repo-root", temporary.Root],
            TestContext.Current.CancellationToken);
        using var evidence = new EvidenceWorkspaces(result);

        // Assert
        result.ExitCode.ShouldBe(0, result.Error);
        var recorded = CandidateManifest.Load(manifest).Evidence;
        var platform = OperatingSystem.IsWindows() ? "windows" : "linux";
        recorded[platform + "PackageConsumptionFrameworks"]!.AsArray().Select(static x => x!.GetValue<string>()).ShouldBe([TestTargetFramework.Moniker]);
    }

    [Fact]
    public static async Task A_repository_without_a_framework_setting_fails_naming_its_props_file()
    {
        // Arrange
        using var temporary = new TemporaryReleasePrepDirectory();
        var manifest = temporary.WriteCandidate();
        await temporary.WriteConsumptionSamples("Consumed sample", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(temporary.Root, "Directory.Build.props"), "<Project />", TestContext.Current.CancellationToken);

        // Act
        var result = await VersioningCommandLineRunner.Invoke(
            ["consume-candidate", "--candidate", manifest, "--repo-root", temporary.Root],
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldNotBe(0);
        result.Error.ShouldContain(Path.Combine(temporary.Root, "Directory.Build.props"), StringComparison.Ordinal);
    }

    [Fact]
    public static async Task A_failing_framework_is_named_and_recorded_as_failed()
    {
        // Arrange
        using var temporary = new TemporaryReleasePrepDirectory();
        var manifest = temporary.WriteCandidate();
        await temporary.WriteConsumptionSamples("Consumed sample", TestContext.Current.CancellationToken);

        // Act
        var result = await VersioningCommandLineRunner.Invoke(
            ["consume-candidate", "--candidate", manifest, "--repo-root", temporary.Root, "--framework", "net10.0", "--framework", "net99.0"],
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain("Package consumption failed for net99.0", StringComparison.Ordinal);
        var recorded = CandidateManifest.Load(manifest).Evidence;
        var platform = OperatingSystem.IsWindows() ? "windows" : "linux";
        recorded[platform + "PackageConsumption"]!.GetValue<string>().ShouldBe("failed");
        recorded.ContainsKey(platform + "PackageConsumptionFrameworks").ShouldBeFalse();
    }
}
