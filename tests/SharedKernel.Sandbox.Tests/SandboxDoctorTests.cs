namespace PandaBear.Sandbox.Tests;

/// <summary>Tests the doctor against a scripted environment.</summary>
[Trait("Capability", "Sandbox")]
public sealed class SandboxDoctorTests
{
    [Fact]
    public void Probe_runs_the_capabilities_then_one_protection_probe_per_denied_path_with_the_home_folder_expanded()
    {
        // Arrange
        var environment = new FakeSandboxEnvironment();
        environment.Reads["/home/user/.ssh"] = (ProbeOutcome.Denied, "refused");
        var configuration = new SandboxConfiguration(1, ["~/.ssh", "/etc/secret"]);

        // Act
        var results = new SandboxDoctor(environment).Probe(configuration);

        // Assert
        results.Select(static result => result.Id).ToArray().ShouldBe(["unix-socket", "named-pipe", "temp-write", "nuget-write", "denied-read ~/.ssh", "denied-read /etc/secret"]);
        environment.Calls.ShouldBe(["socket", "pipe", "write /tmp/x", "write /home/user/.nuget/packages", "read /home/user/.ssh", "read /etc/secret"]);
        results[4].IsGood.ShouldBeTrue();
        results[4].Word.ShouldBe("blocked");
        results[5].IsSkipped.ShouldBeTrue();
    }

    [Fact]
    public void Denied_sockets_are_reported_but_not_required()
    {
        // Arrange
        var environment = new FakeSandboxEnvironment { Socket = (ProbeOutcome.Denied, "no"), Pipe = (ProbeOutcome.Denied, "no") };

        // Act
        var results = new SandboxDoctor(environment).Probe(null);

        // Assert
        results.Take(2).ShouldAllSatisfy(static result => result.Fails.ShouldBeFalse());
        results.Take(2).ShouldAllSatisfy(static result => result.Advice.ShouldNotBeNull());
    }

    [Fact]
    public void A_missing_temporary_folder_fails_but_an_unknown_nuget_folder_is_skipped()
    {
        // Arrange
        var environment = new FakeSandboxEnvironment { NuGetPackagesDirectory = null };
        environment.Writes["/tmp/x"] = (ProbeOutcome.Skipped, "missing");

        // Act
        var results = new SandboxDoctor(environment).Probe(null);

        // Assert
        results[2].Outcome.ShouldBe(ProbeOutcome.Failed);
        results[2].Fails.ShouldBeTrue();
        results[3].IsSkipped.ShouldBeTrue();
        results[3].Fails.ShouldBeFalse();
    }

    [Fact]
    public async Task Run_writes_a_line_per_probe_and_a_summary_and_exits_one_for_an_exposed_path()
    {
        // Arrange
        var environment = new FakeSandboxEnvironment();
        environment.Reads["/etc/secret"] = (ProbeOutcome.Allowed, "readable");
        using var output = new InvariantStringWriter();

        // Act
        var exitCode = await new SandboxDoctor(environment).Run(new SandboxConfiguration(1, ["/etc/secret"]), output, TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        output.ToString().ShouldBe(
            "allowed unix-socket: socket ok\n"
            + "allowed named-pipe: pipe ok\n"
            + "allowed temp-write: write ok\n"
            + "allowed nuget-write: write ok\n"
            + "exposed denied-read /etc/secret: readable\n"
            + "  -> This path is readable from the sandbox; add it to the denied reads.\n"
            + "5 probes: 4 good, 1 not good, 0 skipped.\n");
    }

    [Fact]
    public void A_null_environment_is_refused()
    {
        // Act
        var exception = new Func<object?>(static () => new SandboxDoctor(null!)).ShouldThrow<ArgumentNullException>();

        // Assert
        exception.ParamName.ShouldBe("environment");
    }
}
