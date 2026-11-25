using System.ComponentModel;

namespace SharedKernel.Repositories.Tests;

/// <summary>Engine tests split from the command-line tests in TextEncodingProcessTests.</summary>
public sealed class TextEncodingVerifierTests
{
    [Fact]
    public async Task Cleanup_observation_is_bounded_when_the_task_never_completes()
    {
        // Arrange
        TaskCompletionSource pending = new(TaskCreationOptions.RunContinuationsAsynchronously);

        // Act
        var observation = TextEncodingVerifier.ObserveCleanup(pending.Task, TimeSpan.Zero);

        // Assert
        observation.IsCompleted.ShouldBeTrue();
        await observation;
        pending.Task.IsCompleted.ShouldBeFalse();
    }

    [Theory]
    [InlineData("win32")]
    [InlineData("unauthorized")]
    public async Task Cleanup_observation_swallows_process_cleanup_faults(string faultKind)
    {
        // Arrange
        Exception exception = faultKind == "win32"
            ? new Win32Exception("cleanup failed")
            : new UnauthorizedAccessException("cleanup failed");
        var cleanup = Task.FromException(exception);

        // Act
        await TextEncodingVerifier.ObserveCleanup(cleanup, TimeSpan.Zero);

        // Assert
        cleanup.IsFaulted.ShouldBeTrue();
    }
}
