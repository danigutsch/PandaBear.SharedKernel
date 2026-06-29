namespace SharedKernel.BuildingBlocks.Tests;

public sealed class ExceptionCancellationExtensionsTests
{
    [Fact]
    public void ShouldHandleAsFailure_returns_false_for_operation_cancellation_when_token_is_cancelled()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var exception = new OperationCanceledException(cancellation.Token);

        // Act
        var shouldHandle = exception.ShouldHandleAsFailure(cancellation.Token);

        // Assert
        Assert.False(shouldHandle);
    }

    [Fact]
    public void ShouldHandleAsFailure_returns_true_for_operation_cancellation_when_token_is_not_cancelled()
    {
        // Arrange
        var exception = new OperationCanceledException();

        // Act
        var shouldHandle = exception.ShouldHandleAsFailure(CancellationToken.None);

        // Assert
        Assert.True(shouldHandle);
    }

    [Fact]
    public void ShouldHandleAsFailure_returns_true_for_non_cancellation_exception()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var exception = new InvalidOperationException("failed");

        // Act
        var shouldHandle = exception.ShouldHandleAsFailure(cancellation.Token);

        // Assert
        Assert.True(shouldHandle);
    }
}
