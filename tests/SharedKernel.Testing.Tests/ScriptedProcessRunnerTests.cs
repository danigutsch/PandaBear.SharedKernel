using SharedKernel.Processes;

namespace SharedKernel.Testing.Tests;

/// <summary>Tests the process runner that answers runs from a script and records them.</summary>
public sealed class ScriptedProcessRunnerTests
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task InOrder_answers_each_run_with_the_next_output_and_records_it()
    {
        // Arrange
        var runner = ScriptedProcessRunner.InOrder(new ProcessOutput(0, "first"), new ProcessOutput(1, "second"));
        using var forward = new InvariantStringWriter();

        // Act
        var first = await runner.Run(ProcessStarts.Of("/usr/bin/git", "status"), null, Cancel);
        var second = await runner.Run(ProcessStarts.Of("/usr/bin/dotnet", "build", "-c", "Release"), forward, Cancel);

        // Assert
        first.StandardOutput.ShouldBe("first");
        second.ExitCode.ShouldBe(1);
        runner.Runs.Select(static run => run.FileName).ShouldBe(["/usr/bin/git", "/usr/bin/dotnet"]);
        runner.Runs[1].Arguments.ShouldBe(["build", "-c", "Release"]);
        runner.Runs[0].Forward.ShouldBeNull();
        runner.Runs[1].Forward.ShouldBeSameAs(forward);
    }

    [Fact]
    public void InOrder_rejects_a_run_beyond_the_script()
    {
        // Arrange
        var runner = ScriptedProcessRunner.InOrder();
        Func<object?> run = () => runner.Run(ProcessStarts.Of("/usr/bin/git"), null, Cancel);

        // Act
        var exception = run.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("Run 1 of /usr/bin/git has no scripted answer; the script has 0.");
    }

    [Fact]
    public async Task AnswersInOrder_passes_the_start_information_and_the_token_to_each_answer()
    {
        // Arrange
        using var source = new CancellationTokenSource();
        CancellationToken seen = default;
        var runner = ScriptedProcessRunner.AnswersInOrder((startInfo, token) =>
        {
            seen = token;
            return new ProcessOutput(0, startInfo.ArgumentList[0]);
        });

        // Act
        var output = await runner.Run(ProcessStarts.Of("/usr/bin/echo", "hello"), null, source.Token);

        // Assert
        output.StandardOutput.ShouldBe("hello");
        seen.ShouldBe(source.Token);
    }

    [Fact]
    public async Task ByFileName_answers_by_executable_and_reports_an_unscripted_one()
    {
        // Arrange
        var runner = ScriptedProcessRunner.ByFileName(new Dictionary<string, ProcessOutput> { ["/usr/bin/git"] = new(0, "git") });
        Func<object?> unscripted = () => runner.Run(ProcessStarts.Of("/usr/bin/svn"), null, Cancel);

        // Act
        var output = await runner.Run(ProcessStarts.Of("/usr/bin/git"), null, Cancel);
        var exception = unscripted.ShouldThrow<KeyNotFoundException>();

        // Assert
        output.StandardOutput.ShouldBe("git");
        exception.Message.ShouldBe("No output is scripted for /usr/bin/svn.");
        runner.Runs.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Returning_answers_every_run_with_the_same_output()
    {
        // Arrange
        var expected = new ProcessOutput(3, "out", "err");
        var runner = ScriptedProcessRunner.Returning(expected);

        // Act
        var first = await runner.Run(ProcessStarts.Of("/a"), null, Cancel);
        var second = await runner.Run(ProcessStarts.Of("/b"), null, Cancel);

        // Assert
        first.ShouldBeSameAs(expected);
        second.ShouldBeSameAs(expected);
    }

    [Fact]
    public void Throwing_throws_synchronously_and_still_records_the_run()
    {
        // Arrange
        var failure = new InvalidOperationException("cannot start");
        var runner = ScriptedProcessRunner.Throwing(failure);
        Func<object?> run = () => runner.Run(ProcessStarts.Of("/usr/bin/git"), null, Cancel);

        // Act
        var exception = run.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.ShouldBeSameAs(failure);
        runner.Runs.ShouldHaveSingleItem().FileName.ShouldBe("/usr/bin/git");
    }

    [Fact]
    public async Task Answering_answers_from_the_recorded_run()
    {
        // Arrange
        var runner = ScriptedProcessRunner.Answering(static run => new ProcessOutput(0, run.FileName + " " + string.Join(' ', run.Arguments)));

        // Act
        var output = await runner.Run(ProcessStarts.Of("/usr/bin/git", "rev-parse", "HEAD"), null, Cancel);

        // Assert
        output.StandardOutput.ShouldBe("/usr/bin/git rev-parse HEAD");
    }

    [Fact]
    public async Task A_run_keeps_the_arguments_it_was_started_with()
    {
        // Arrange
        var runner = ScriptedProcessRunner.Returning(new ProcessOutput(0, string.Empty));
        var startInfo = ProcessStarts.Of("/usr/bin/git", "status");

        // Act
        await runner.Run(startInfo, null, Cancel);
        startInfo.ArgumentList.Add("--short");

        // Assert
        runner.Runs.ShouldHaveSingleItem().Arguments.ShouldBe(["status"]);
        runner.Runs[0].StartInfo.ShouldBeSameAs(startInfo);
    }
}
