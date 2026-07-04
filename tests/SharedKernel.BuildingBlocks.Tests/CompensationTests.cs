using SharedKernel.Testing.Assertions;

namespace SharedKernel.BuildingBlocks.Tests;

public sealed class CompensationTests
{
    [Fact]
    public async Task CompleteOrCompensate_runs_compensation_when_operation_fails()
    {
        // Arrange
        var compensated = false;

        // Act
        var action = () => Compensation.CompleteOrCompensate(
            ct => throw new InvalidOperationException("operation failed"),
            ct =>
            {
                compensated = true;
                return ValueTask.CompletedTask;
            },
            TestContext.Current.CancellationToken).AsTask();

        var exception = await action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("operation failed");
        compensated.ShouldBeTrue();
    }

    [Fact]
    public async Task CompleteOrCompensate_does_not_run_compensation_when_operation_succeeds()
    {
        // Arrange
        var compensated = false;

        // Act
        await Compensation.CompleteOrCompensate(
            ct => ValueTask.CompletedTask,
            ct =>
            {
                compensated = true;
                return ValueTask.CompletedTask;
            },
            TestContext.Current.CancellationToken);

        // Assert
        compensated.ShouldBeFalse();
    }

    [Fact]
    public async Task CompleteOrCompensate_preserves_operation_failure_when_compensation_fails()
    {
        // Arrange
        var operationException = new InvalidOperationException("operation failed");

        // Act
        var action = () => Compensation.CompleteOrCompensate(
            ct => throw operationException,
            ct => throw new TimeoutException("compensation failed"),
            TestContext.Current.CancellationToken).AsTask();

        var exception = await action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.ShouldBeSameAs(operationException);
    }

    [Fact]
    public async Task CompleteOrCompensate_does_not_compensate_cooperative_cancellation()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var compensated = false;

        // Act
        var action = () => Compensation.CompleteOrCompensate(
            ct => throw new OperationCanceledException(ct),
            ct =>
            {
                compensated = true;
                return ValueTask.CompletedTask;
            },
            cancellation.Token).AsTask();

        var exception = await action.ShouldThrow<OperationCanceledException>();

        // Assert
        exception.CancellationToken.ShouldBe(cancellation.Token);
        compensated.ShouldBeFalse();
    }
}
