using PandaBear.Versioning.Tool;

namespace PandaBear.Versioning.Tests;

[Trait(Testing.SharedKernelTestTraitNames.CapabilityName, TestTraits.VersioningCapability)]
public static class CandidateStagingTests
{
    [Fact]
    public static async Task Stages_verified_packages_idempotently_to_a_relocated_feed()
    {
        // Arrange
        using var temporary = new TemporaryReleasePrepDirectory();
        var manifest = temporary.WriteCandidate();

        // Act
        await CandidateStagingCommand.Stage(manifest, temporary.PackageDirectory, temporary.OutputDirectory, TestContext.Current.CancellationToken);
        await CandidateStagingCommand.Stage(manifest, temporary.PackageDirectory, temporary.OutputDirectory, TestContext.Current.CancellationToken);

        // Assert
        var staged = CandidateManifest.ReadAndVerify(Path.Combine(temporary.OutputDirectory, "candidate.json"));
        staged["packageVersion"]?.GetValue<string>().ShouldBe("1.0.0");
        Directory.GetFiles(temporary.OutputDirectory, "*.tmp").ShouldBeEmpty();
    }

    [Fact]
    public static async Task Conflicting_destination_is_preserved()
    {
        // Arrange
        using var temporary = new TemporaryReleasePrepDirectory();
        var manifest = temporary.WriteCandidate();
        Directory.CreateDirectory(temporary.OutputDirectory);
        var target = Path.Combine(temporary.OutputDirectory, "PandaBear.SharedKernel.Probe.1.0.0.nupkg");
        await File.WriteAllTextAsync(target, "existing artifact", TestContext.Current.CancellationToken);

        // Act
        Func<Task> stage = () => CandidateStagingCommand.Stage(manifest, temporary.PackageDirectory, temporary.OutputDirectory, TestContext.Current.CancellationToken);
        var error = await stage.ShouldThrow<ArgumentException>();

        // Assert
        error.Message.ShouldContain("Refusing to overwrite", StringComparison.Ordinal);
        var content = await File.ReadAllTextAsync(target, TestContext.Current.CancellationToken);
        content.ShouldBe("existing artifact");
        File.Exists(Path.Combine(temporary.OutputDirectory, "candidate.json")).ShouldBeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public static async Task Missing_or_tampered_source_cannot_create_a_feed(bool tampered)
    {
        // Arrange
        using var temporary = new TemporaryReleasePrepDirectory();
        var manifest = temporary.WriteCandidate();
        var package = Directory.GetFiles(temporary.PackageDirectory, "*.nupkg").Single();
        if (tampered) { await File.WriteAllTextAsync(package, "tampered", TestContext.Current.CancellationToken); }
        else { File.Delete(package); }

        // Act
        Func<Task> stage = () => CandidateStagingCommand.Stage(manifest, temporary.PackageDirectory, temporary.OutputDirectory, TestContext.Current.CancellationToken);
        await stage.ShouldThrowAssignableTo<Exception>();

        // Assert
        Directory.Exists(temporary.OutputDirectory).ShouldBeFalse();
    }

    [Fact]
    public static async Task Cancellation_before_staging_leaves_no_destination()
    {
        // Arrange
        using var temporary = new TemporaryReleasePrepDirectory();
        var manifest = temporary.WriteCandidate();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        // Act
        Func<Task> stage = () => CandidateStagingCommand.Stage(manifest, temporary.PackageDirectory, temporary.OutputDirectory, cancellation.Token);
        await stage.ShouldThrow<OperationCanceledException>();

        // Assert
        Directory.Exists(temporary.OutputDirectory).ShouldBeFalse();
    }
}
