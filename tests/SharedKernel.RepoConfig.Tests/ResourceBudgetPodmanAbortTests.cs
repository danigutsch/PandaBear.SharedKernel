using System.Diagnostics;
using SharedKernel.Processes;

namespace SharedKernel.RepoConfig.Tests;

/// <summary>Checks the cooperative abort deadlines and the private folder ownership boundary without an engine.</summary>
[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
public sealed class ResourceBudgetPodmanAbortTests
{
    /// <summary>Gets whether the Linux ownership checks are supported by the test host.</summary>
    public static bool Linux => OperatingSystem.IsLinux();

    [Theory(SkipUnless = nameof(Linux), Skip = "The cooperative process test requires Linux.")]
    [InlineData(false, "Unacknowledged", 6)]
    [InlineData(true, "TimedOut", 141)]
    public async Task An_unresponsive_participant_gets_only_its_own_deadline(
        bool acknowledges, string expected, int seconds)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var directory = new TemporaryDirectory();
        var start = new ProcessStartInfo("/bin/sleep") { UseShellExecute = false, ArgumentList = { "30" } };
        start.Environment["TMPDIR"] = directory.Path;
        using var rendezvous = ResourceBudgetPodmanAbort.Create(start);
        var scope = Directory.GetDirectories(directory.Path).ShouldHaveSingleItem();
        var participant = Path.Combine(scope, "podman-" + Guid.NewGuid().ToString("N"));
        await File.WriteAllTextAsync(participant, string.Empty, ct);
        if (acknowledges)
        {
            await File.WriteAllTextAsync(participant + ".acknowledged", string.Empty, ct);
        }

        // Act
        var result = await ResourceBudgetProcessFixture.TripPodmanDeadline(rendezvous, start, TimeSpan.FromSeconds(seconds), ct);

        // Assert
        result.Outcome.ToString().ShouldBe(expected);
        File.Exists(Path.Combine(scope, "stop")).ShouldBeTrue();
        File.Exists(participant).ShouldBeTrue();
        result.ChildAlive.ShouldBeTrue();
    }

    [Theory(SkipUnless = nameof(Linux), Skip = "The cooperative ownership test requires Linux.")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_replaced_rendezvous_leaves_an_unrelated_directory_untouched(bool requestStop)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var directory = new TemporaryDirectory();
        var start = new ProcessStartInfo("/bin/true") { UseShellExecute = false };
        start.Environment["TMPDIR"] = directory.Path;
        string witness;
        string unrelated;
        string outcome;
        using (var rendezvous = ResourceBudgetPodmanAbort.Create(start))
        {
            var scope = Directory.GetDirectories(directory.Path).ShouldHaveSingleItem();
            Directory.Move(scope, Path.Combine(directory.Path, "original"));
            unrelated = Directory.CreateDirectory(Path.Combine(directory.Path, "unrelated")).FullName;
            witness = Path.Combine(unrelated, "owned-by-caller");
            await File.WriteAllTextAsync(witness, "unchanged", ct);
            Directory.CreateSymbolicLink(scope, unrelated);
            using var child = ProcessTree.Start(start);

            // Act
            outcome = requestStop ? (await rendezvous.RequestStop(child)).ToString() : "Disposed";
            await child.WaitForExitAsync(ct);
        }

        // Assert
        outcome.ShouldBe(requestStop ? "Unavailable" : "Disposed");
        File.Exists(witness).ShouldBeTrue();
        Directory.GetFiles(unrelated).ShouldBe([witness]);
    }

    [Theory(SkipUnless = nameof(Linux), Skip = "The cooperative receipt test requires Linux.")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_natural_adverse_engine_receipt_remains_in_the_private_directory(bool requestStop)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var directory = new TemporaryDirectory();
        var start = new ProcessStartInfo("/bin/true") { UseShellExecute = false };
        start.Environment["TMPDIR"] = directory.Path;

        // Act
        string result;
        string outcome;
        using (var rendezvous = ResourceBudgetPodmanAbort.Create(start))
        {
            var scope = Directory.GetDirectories(directory.Path).ShouldHaveSingleItem();
            result = Path.Combine(scope, "podman-" + Guid.NewGuid().ToString("N") + ".unavailable");
            await File.WriteAllTextAsync(result, string.Empty, ct);
            using var child = ProcessTree.Start(start);
            outcome = requestStop ? (await rendezvous.RequestStop(child)).ToString() : "Disposed";
            await child.WaitForExitAsync(ct);
        }

        // Assert
        outcome.ShouldBe(requestStop ? "Unavailable" : "Disposed");
        File.Exists(result).ShouldBeTrue();
    }

    [Theory(SkipUnless = nameof(Linux), Skip = "The cooperative start test requires Linux.")]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task Invalid_scratch_or_failed_process_start_preserves_launch_failure_and_retires_the_control_directory(
        bool relativeScratch, bool missingScratch)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var directory = new TemporaryDirectory();
        var start = new ProcessStartInfo(Path.Combine(directory.Path, "missing-command")) { UseShellExecute = false };
        start.Environment["TMPDIR"] = relativeScratch ? "relative"
            : missingScratch ? Path.Combine(directory.Path, "missing-scratch") : directory.Path;

        // Act
        var result = await ResourceBudgetProcess.Run(start, cooperativePodmanCleanup: true, ct);

        // Assert
        result.Started.ShouldBeFalse();
        result.ExitCode.ShouldBeNull();
        Directory.EnumerateDirectories(directory.Path).ShouldBeEmpty();
    }

    [Theory(SkipUnless = nameof(Linux), Skip = "The cooperative participant test requires Linux.")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Invalid_inherited_cleanup_path_fails_before_launching_the_engine(bool absolute)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var directory = new TemporaryDirectory();
        string[] arguments =
        [
            typeof(RepoConfigCommandLine).Assembly.Location, "run-isolated-podman",
            "--image-store", directory.Path, "--", "/bin/true",
        ];
        var environment = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["SHAREDKERNEL_REPO_BUDGET_ABORT"] = absolute ? directory.Path : "relative",
        };

        // Act
        var result = await TestProcess.Run("dotnet", directory.Path, arguments, ct, environment);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("The Podman budget cleanup directory is invalid.", StringComparison.Ordinal);
        Directory.EnumerateDirectories(directory.Path).ShouldBeEmpty();
    }
}
