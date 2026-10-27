using System.Text.RegularExpressions;
using PandaBear.VaultManager.Tool.Secrets;

namespace PandaBear.VaultManager.Tool.Tests.Secrets;

/// <summary>Tests that a regex match is retried once after a timeout.</summary>
public sealed class PatternRetryTests
{
    /// <summary>A match that completes runs once and returns its result.</summary>
    [Fact]
    public void TryRun_runs_a_match_that_completes_once()
    {
        // Arrange
        int runs = 0;

        // Act
        bool completed = PatternRetry.TryRun(() => ++runs, out int result);

        // Assert
        completed.ShouldBeTrue();
        result.ShouldBe(1);
        runs.ShouldBe(1);
    }

    /// <summary>A match that times out once runs again and returns the second result.</summary>
    [Fact]
    public void TryRun_runs_a_match_that_times_out_once_again_and_returns_the_second_result()
    {
        // Arrange
        int runs = 0;

        // Act
        bool completed = PatternRetry.TryRun(() => ++runs == 1 ? throw new RegexMatchTimeoutException() : runs, out int result);

        // Assert
        completed.ShouldBeTrue();
        result.ShouldBe(2);
        runs.ShouldBe(2);
    }

    /// <summary>A match that times out twice is reported as incomplete and is not run a third time.</summary>
    [Fact]
    public void TryRun_reports_a_match_that_times_out_twice_without_running_it_a_third_time()
    {
        // Arrange
        int runs = 0;

        // Act
        bool completed = PatternRetry.TryRun(() => ++runs > 0 ? throw new RegexMatchTimeoutException() : runs, out _);

        // Assert
        completed.ShouldBeFalse();
        runs.ShouldBe(2);
    }

    /// <summary>Other exceptions pass through and the match is not run again.</summary>
    [Fact]
    public void TryRun_lets_other_exceptions_through_without_running_the_match_again()
    {
        // Arrange
        int runs = 0;
        Action action = () => PatternRetry.TryRun(() => ++runs > 0 ? throw new InvalidOperationException() : runs, out _);

        // Act
        action.ShouldThrow<InvalidOperationException>();

        // Assert
        runs.ShouldBe(1);
    }
}
