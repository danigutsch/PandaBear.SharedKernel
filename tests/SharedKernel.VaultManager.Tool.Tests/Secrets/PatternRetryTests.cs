using System.Text.RegularExpressions;
using PandaBear.VaultManager.Tool.Secrets;

namespace PandaBear.VaultManager.Tool.Tests.Secrets;

public sealed class PatternRetryTests
{
    [Fact]
    public void TryRun_runs_a_match_that_completes_once()
    {
        int runs = 0;

        bool completed = PatternRetry.TryRun(() => ++runs, out int result);

        completed.ShouldBeTrue();
        result.ShouldBe(1);
        runs.ShouldBe(1);
    }

    [Fact]
    public void TryRun_runs_a_match_that_times_out_once_again_and_returns_the_second_result()
    {
        int runs = 0;

        bool completed = PatternRetry.TryRun(() => ++runs == 1 ? throw new RegexMatchTimeoutException() : runs, out int result);

        completed.ShouldBeTrue();
        result.ShouldBe(2);
        runs.ShouldBe(2);
    }

    [Fact]
    public void TryRun_reports_a_match_that_times_out_twice_without_running_it_a_third_time()
    {
        int runs = 0;

        bool completed = PatternRetry.TryRun(() => ++runs > 0 ? throw new RegexMatchTimeoutException() : runs, out _);

        completed.ShouldBeFalse();
        runs.ShouldBe(2);
    }

    [Fact]
    public void TryRun_lets_other_exceptions_through_without_running_the_match_again()
    {
        int runs = 0;

        Action action = () => PatternRetry.TryRun(() => ++runs > 0 ? throw new InvalidOperationException() : runs, out _);

        action.ShouldThrow<InvalidOperationException>();
        runs.ShouldBe(1);
    }
}
