namespace SharedKernel.BuildingBlocks.Tests;

public sealed class CompensationTests
{
    [Fact]
    public async Task CompleteOrCompensate_runs_compensation_when_operation_fails()
    {
        // Arrange
        var compensated = false;

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () => await Compensation.CompleteOrCompensate(
            _ => throw new InvalidOperationException("operation failed"),
            _ =>
            {
                compensated = true;
                return ValueTask.CompletedTask;
            },
            TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal("operation failed", exception.Message);
        Assert.True(compensated);
    }

    [Fact]
    public async Task CompleteOrCompensate_does_not_run_compensation_when_operation_succeeds()
    {
        // Arrange
        var compensated = false;

        // Act
        await Compensation.CompleteOrCompensate(
            _ => ValueTask.CompletedTask,
            _ =>
            {
                compensated = true;
                return ValueTask.CompletedTask;
            },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(compensated);
    }

    [Fact]
    public async Task CompleteOrCompensate_preserves_operation_failure_when_compensation_fails()
    {
        // Arrange
        var operationException = new InvalidOperationException("operation failed");

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () => await Compensation.CompleteOrCompensate(
            _ => throw operationException,
            _ => throw new TimeoutException("compensation failed"),
            TestContext.Current.CancellationToken));

        // Assert
        Assert.Same(operationException, exception);
    }

    [Fact]
    public async Task CompleteOrCompensate_does_not_compensate_cooperative_cancellation()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var compensated = false;

        // Act
        var exception = await Assert.ThrowsAsync<OperationCanceledException>(async () => await Compensation.CompleteOrCompensate(
            token => throw new OperationCanceledException(token),
            _ =>
            {
                compensated = true;
                return ValueTask.CompletedTask;
            },
            cancellation.Token));

        // Assert
        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.False(compensated);
    }
}
