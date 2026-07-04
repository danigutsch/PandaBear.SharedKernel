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
}
