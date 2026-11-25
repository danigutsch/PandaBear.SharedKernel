namespace SharedKernel.Secrets.Tests;

/// <summary>Tests the run helper that every provider uses.</summary>
[Trait(TestTraitNames.CapabilityName, "Secrets")]
public sealed class ToolRunsTests
{
    private static readonly ToolRequest Request = new(Environment.ProcessPath!, []);

    [Fact]
    public async Task A_run_that_exits_with_zero_is_returned_for_the_caller_to_dispose()
    {
        // Act
        var result = await new StubToolRunner(0, string.Empty).RunToSuccess("sops", Request, TestContext.Current.CancellationToken);

        // Assert
        using var run = result.ShouldBeSuccess();
        run.StandardOutput.RevealUtf8().ShouldBe("output");
    }

    [Fact]
    public async Task Another_exit_code_is_one_line_without_the_output()
    {
        // Act
        var result = await new StubToolRunner(128, "Failed to get the data key\nsecond line").RunToSuccess("sops", Request, TestContext.Current.CancellationToken);

        // Assert
        result.ErrorDetails.ShouldNotBeNull().Detail.ShouldBe("sops exited with code 128: Failed to get the data key");
    }

    [Fact]
    public async Task A_program_that_cannot_run_or_times_out_is_an_error()
    {
        // Act
        var missing = await new StubToolRunner(0, string.Empty, new InvalidOperationException("Failed to start sops.")).RunToSuccess("sops", Request, TestContext.Current.CancellationToken);
        var slow = await new StubToolRunner(0, string.Empty, new TimeoutException("too slow")).RunToSuccess("sops", Request, TestContext.Current.CancellationToken);

        // Assert
        missing.ErrorDetails.ShouldNotBeNull().Detail.ShouldBe("sops could not run: Failed to start sops.");
        slow.ErrorDetails.ShouldNotBeNull().Detail.ShouldBe("sops could not run: too slow");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("relative/file")]
    [InlineData("/a\nb")]
    public void A_path_that_is_not_a_safe_absolute_path_is_refused_as_an_argument(string? path)
    {
        // Assert
        ToolRuns.CheckFileArgument(path).ShouldNotBeNull();
    }

    [Fact]
    public void An_absolute_path_is_accepted_as_an_argument()
    {
        // Assert
        ToolRuns.CheckFileArgument(Environment.ProcessPath).ShouldBeNull();
    }
}
