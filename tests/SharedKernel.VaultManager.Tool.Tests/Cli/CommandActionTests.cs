using System.CommandLine;
using PandaBear.VaultManager.Tool.Cli;
using PandaBear.VaultManager.Tool.Tests.Pipeline;

namespace PandaBear.VaultManager.Tool.Tests.Cli;

/// <summary>Tests how command actions end when they are cancelled.</summary>
public sealed class CommandActionTests
{
    private const string ExportPassword = "fixture export password";
    private const string FilePassword = "fixture file password";

    // Ends a blocked test thread when the code under test is wrong; a passing run never waits for it.
    private static readonly TimeSpan SafetyTimeout = TimeSpan.FromSeconds(30);

    /// <summary>Verifies that cancelled work outside a password prompt ends before the command exits 1.</summary>
    [Fact]
    public async Task Work_canceled_outside_a_password_prompt_ends_before_the_command_exits_1()
    {
        // Arrange
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        using var stopWaitStarted = new ManualResetEventSlim();
        var time = new ManualTimeProvider { OnTimerCreated = stopWaitStarted.Set };
        int ended = 0;
        RootCommand command = ActionCommands.Running(new TestPrompts(ExportPassword).Environment() with
        {
            Time = time
        }, _ =>
        {
            // Ctrl+C arrives while the work writes a file, away from any prompt; the write then completes.
            cancellation.Cancel();
            stopWaitStarted.Wait(SafetyTimeout);
            Volatile.Write(ref ended, 1);
            return ExitCodes.Success;
        });

        // Act
        var run = await CommandLineRun.Invoke(command, [], cancellation.Token);
        bool endedBeforeExit = Volatile.Read(ref ended) == 1;
        stopWaitStarted.Set();

        // Assert
        run.ExitCode.ShouldBe(1);
        endedBeforeExit.ShouldBeTrue();
    }

    /// <summary>Verifies that work that keeps running after cancellation is waited for, but for less than the process termination timeout.</summary>
    [Fact]
    public async Task Work_that_keeps_running_after_cancellation_is_waited_for_less_than_the_process_termination_timeout()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        using var release = new ManualResetEventSlim();
        var time = new ManualTimeProvider();
        RootCommand command = ActionCommands.Running(new TestPrompts(ExportPassword).Environment() with
        {
            Time = time
        }, _ =>
        {
            cancellation.Cancel();
            release.Wait(SafetyTimeout);
            return ExitCodes.Success;
        });

        Task<CommandLineRun> invocation = CommandLineRun.Invoke(command, [], cancellation.Token);
        Task first = await Task.WhenAny(invocation, time.FirstTimer).WaitAsync(SafetyTimeout, TestContext.Current.CancellationToken);
        first.ShouldBeSameAs(time.FirstTimer);
        TimeSpan stopTimeout = await time.FirstTimer;
        bool exitedBeforeTheTimeout = invocation.IsCompleted;
        time.Advance(stopTimeout);
        var run = await invocation.WaitAsync(SafetyTimeout, TestContext.Current.CancellationToken);
        release.Set();

        exitedBeforeTheTimeout.ShouldBeFalse();
        run.ExitCode.ShouldBe(1);
        stopTimeout.ShouldBeLessThan(new InvocationConfiguration().ProcessTerminationTimeout.ShouldNotBeNull());
    }

    /// <summary>Verifies that cancellation at a password prompt exits 1 without waiting for the answer.</summary>
    [Fact]
    public async Task Work_canceled_at_a_password_prompt_exits_1_without_waiting_for_the_answer()
    {
        // Arrange
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        using var answer = new ManualResetEventSlim();
        var time = new ManualTimeProvider();
        int promptReturned = 0;
        var environment = new TestPrompts(ExportPassword).Environment() with
        {
            Time = time,
            ReadPassword = _ =>
            {
                // Ctrl+C arrives while the prompt waits for the user.
                cancellation.Cancel();
                answer.Wait(SafetyTimeout);
                Volatile.Write(ref promptReturned, 1);
                return FilePassword;
            },
        };
        RootCommand command = ActionCommands.Running(environment, prompts => prompts.Environment.ReadPassword("Password for protected.json").Length);

        // Act
        var run = await CommandLineRun.Invoke(command, [], cancellation.Token);
        bool exitedWhilePromptWaited = Volatile.Read(ref promptReturned) == 0;
        answer.Set();

        // Assert
        run.ExitCode.ShouldBe(1);
        exitedWhilePromptWaited.ShouldBeTrue();
        time.FirstTimer.IsCompleted.ShouldBeFalse();
    }
}
