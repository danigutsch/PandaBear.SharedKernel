using System.Text;

namespace PandaBear.Repository.Tests;

/// <summary>Tests staging pinned candidate packages from a local feed directory into the output directory.</summary>
public sealed class CandidateStagingFeedTests
{
    [Fact]
    public async Task Feed_staging_copies_the_pinned_packages_and_writes_the_pin_bytes_last()
    {
        // Arrange
        using var scope = new CandidateStagingScope();

        // The pin keeps its own formatting and evidence, which candidate.json must reproduce byte for byte.
        scope.WritePin(Encoding.UTF8.GetString(scope.Pin).Replace("\n", "\r\n", StringComparison.Ordinal));
        var git = new CandidateGit();
        using var server = new FakeReleaseServer();

        // Act
        var result = await RepositoryToolInvocation.Run(
            scope.Base, scope.Arguments(), new ProcessLog().Run, CandidateStagingTestServices.Create(git, server), TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(0, result.Error);
        Directory.GetFiles(scope.Output).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToArray()
            .ShouldBe([.. scope.PackageNames.Append("candidate.json").Order(StringComparer.Ordinal)], "Only the pinned packages and the pin are staged.");
        foreach (var (name, content) in scope.Packages)
        {
            var staged = await File.ReadAllBytesAsync(Path.Combine(scope.Output, name), TestContext.Current.CancellationToken);
            staged.ShouldBe(content);
        }

        var manifest = await File.ReadAllBytesAsync(Path.Combine(scope.Output, "candidate.json"), TestContext.Current.CancellationToken);
        manifest.ShouldBe(scope.Pin, "candidate.json is the pin's exact bytes.");
        git.Commands.ShouldBe(["rev-parse --is-inside-work-tree --show-prefix", "rev-parse --verify HEAD", "status --porcelain"]);
        git.Calls.ShouldAllSatisfy(call => call.WorkingDirectory.ShouldBe(scope.LibraryRoot));
        server.Requests.ShouldBeEmpty();
        scope.ScratchDirectories().ShouldBeEmpty();
        result.Output.ShouldBe($"Staged {CandidateStagingScope.Version} in {scope.Output}: 2 packages added, 0 already present." + Environment.NewLine);
    }

    [Fact]
    public async Task Feed_staging_repeats_without_changes_and_keeps_other_versions()
    {
        // Arrange
        using var scope = new CandidateStagingScope();
        var services = CandidateStagingTestServices.Offline(new CandidateGit());
        Directory.CreateDirectory(scope.Output);
        var otherVersion = Path.Combine(scope.Output, "PandaBear.SharedKernel.Results.1.0.0.nupkg");
        await File.WriteAllTextAsync(otherVersion, "previous candidate", TestContext.Current.CancellationToken);
        var first = await RepositoryToolInvocation.Run(scope.Base, scope.Arguments(), new ProcessLog().Run, services, TestContext.Current.CancellationToken);
        var stagedTimes = scope.PackageNames.Select(name => File.GetLastWriteTimeUtc(Path.Combine(scope.Output, name))).ToArray();

        // Act
        var second = await RepositoryToolInvocation.Run(scope.Base, scope.Arguments(), new ProcessLog().Run, services, TestContext.Current.CancellationToken);

        // Assert
        first.ExitCode.ShouldBe(0, first.Error);
        second.ExitCode.ShouldBe(0, second.Error);
        second.Output.ShouldContain("0 packages added, 2 already present.", StringComparison.Ordinal);
        scope.PackageNames.Select(name => File.GetLastWriteTimeUtc(Path.Combine(scope.Output, name))).ShouldBe(stagedTimes, "Identical packages stay in place.");
        File.Exists(otherVersion).ShouldBeTrue("Staging never prunes other versions.");
        scope.ScratchDirectories().ShouldBeEmpty();
    }

    [Fact]
    public async Task Feed_staging_refuses_to_overwrite_a_different_package()
    {
        // Arrange
        using var scope = new CandidateStagingScope();
        Directory.CreateDirectory(scope.Output);
        var existing = Path.Combine(scope.Output, scope.PackageNames[0]);
        await File.WriteAllTextAsync(existing, "different content", TestContext.Current.CancellationToken);

        // Act
        var result = await RepositoryToolInvocation.Run(
            scope.Base, scope.Arguments(), new ProcessLog().Run, CandidateStagingTestServices.Offline(new CandidateGit()), TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain("Refusing to overwrite " + existing, StringComparison.Ordinal);
        var content = await File.ReadAllTextAsync(existing, TestContext.Current.CancellationToken);
        content.ShouldBe("different content");
        Directory.GetFiles(scope.Output).ShouldBe([existing], "Nothing is staged when a package conflicts.");
        scope.ScratchDirectories().ShouldBeEmpty();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Feed_staging_rejects_a_tampered_or_missing_package_before_changing_the_output(bool tampered)
    {
        // Arrange
        using var scope = new CandidateStagingScope();
        var package = Path.Combine(scope.Feed, scope.PackageNames[1]);
        if (tampered)
        {
            await File.AppendAllTextAsync(package, "tampered", TestContext.Current.CancellationToken);
        }
        else
        {
            File.Delete(package);
        }

        // Act
        var result = await RepositoryToolInvocation.Run(
            scope.Base, scope.Arguments(), new ProcessLog().Run, CandidateStagingTestServices.Offline(new CandidateGit()), TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain(scope.PackageNames[1], StringComparison.Ordinal);
        Directory.Exists(scope.Output).ShouldBeFalse("Packages move into the output only after every package is verified.");
        scope.ScratchDirectories().ShouldBeEmpty();
    }

    [Fact]
    public async Task Feed_staging_requires_an_existing_feed_directory()
    {
        // Arrange
        using var scope = new CandidateStagingScope();
        Directory.Delete(scope.Feed, recursive: true);
        var git = new CandidateGit();

        // Act
        var result = await RepositoryToolInvocation.Run(
            scope.Base, scope.Arguments(), new ProcessLog().Run, CandidateStagingTestServices.Offline(git), TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain("The feed directory does not exist: " + scope.Feed, StringComparison.Ordinal);
        git.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task Staging_removes_stale_scratch_directories_and_keeps_live_ones()
    {
        // Arrange
        using var scope = new CandidateStagingScope();
        var parent = Path.GetDirectoryName(scope.Output)!;
        var stale = Directory.CreateDirectory(Path.Combine(parent, ".sharedkernel-feed.stage-" + Guid.NewGuid().ToString("N"))).FullName;
        var live = Directory.CreateDirectory(Path.Combine(parent, ".sharedkernel-feed.stage-" + Guid.NewGuid().ToString("N"))).FullName;
        var unrelated = Directory.CreateDirectory(Path.Combine(parent, ".sharedkernel-feed.stage-keep")).FullName;
        await File.WriteAllTextAsync(Path.Combine(stale, "partial.nupkg"), "partial", TestContext.Current.CancellationToken);
        var twoDaysAgo = TimeProvider.System.GetUtcNow().UtcDateTime.AddDays(-2);
        Directory.SetLastWriteTimeUtc(stale, twoDaysAgo);
        Directory.SetLastWriteTimeUtc(unrelated, twoDaysAgo);

        // Act
        var result = await RepositoryToolInvocation.Run(
            scope.Base, scope.Arguments(), new ProcessLog().Run, CandidateStagingTestServices.Offline(new CandidateGit()), TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(0, result.Error);
        Directory.Exists(stale).ShouldBeFalse("Scratch older than a day is left over from an interrupted run.");
        Directory.Exists(live).ShouldBeTrue("A concurrent stager's recent scratch survives.");
        Directory.Exists(unrelated).ShouldBeTrue("Only names that staging creates are removed.");
    }

    [Fact]
    public async Task Staging_continues_when_a_stale_scratch_directory_cannot_be_deleted()
    {
        // Arrange
        using var scope = new CandidateStagingScope();
        var stale = scope.AddUndeletableStaleScratch();

        // Act
        var result = await RepositoryToolInvocation.Run(
            scope.Base, scope.Arguments(), new ProcessLog().Run, CandidateStagingTestServices.Offline(new CandidateGit()), TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(0, result.Error);
        Directory.GetFiles(scope.Output).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToArray()
            .ShouldBe([.. scope.PackageNames.Append("candidate.json").Order(StringComparer.Ordinal)], "A leftover that cannot be deleted does not stop staging.");
        scope.ScratchDirectories().ShouldNotContain(directory => directory != stale, "The run still deletes its own staging directory.");
    }

    [Fact]
    public async Task Canceled_staging_exits_with_code_1_without_staging_packages()
    {
        // Arrange
        using var scope = new CandidateStagingScope();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        // Act
        var result = await RepositoryToolInvocation.Run(
            scope.Base, scope.Arguments(), new ProcessLog().Run, CandidateStagingTestServices.Offline(new CandidateGit()), cancellation.Token);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.Output.ShouldBe(string.Empty);
        Directory.Exists(scope.Output).ShouldBeFalse();
        scope.ScratchDirectories().ShouldBeEmpty();
    }
}
