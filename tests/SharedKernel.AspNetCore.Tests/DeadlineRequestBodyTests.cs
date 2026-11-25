// Adapted from ViajantesTurismo under its MIT license. See LICENSE.ViajantesTurismo.txt.

namespace SharedKernel.AspNetCore.Tests;

/// <summary>Verifies the read view links cancellation and retains ownership with the caller.</summary>
public sealed class DeadlineRequestBodyTests
{
    [Fact]
    public async Task Reads_preserve_the_underlying_stream_position_and_ownership()
    {
        // Arrange
        await using var body = new MemoryStream([65, 66, 67], writable: false);
        await using var view = new DeadlineRequestBody(body, TestContext.Current.CancellationToken);
        var buffer = new byte[2];

        // Act
        view.Position = 0;
        var firstRead = await view.ReadAsync(buffer.AsMemory(0, 1), TestContext.Current.CancellationToken);
        var secondRead = await view.ReadAsync(buffer.AsMemory(1, 1), CancellationToken.None);
        var position = view.Position;
        var length = view.Length;
        var canRead = view.CanRead;
        var canSeek = view.CanSeek;
        var canWrite = view.CanWrite;
        _ = view.Seek(2, SeekOrigin.Begin);
        await view.DisposeAsync();
        var lastByte = body.ReadByte();

        // Assert
        firstRead.ShouldBe(1);
        secondRead.ShouldBe(1);
        buffer.ShouldBe(new byte[] { 65, 66 });
        position.ShouldBe(2);
        length.ShouldBe(3);
        canRead.ShouldBeTrue();
        canSeek.ShouldBeTrue();
        canWrite.ShouldBeFalse();
        lastByte.ShouldBe(67);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_read_observes_both_the_request_token_and_the_callers_token(bool cancelRequest)
    {
        // Arrange
        await using var body = new MemoryStream([65], writable: false);
        using var request = new CancellationTokenSource();
        using var caller = new CancellationTokenSource();
        await using var view = new DeadlineRequestBody(body, request.Token);
        var buffer = new byte[1];
        var cancelled = cancelRequest ? request : caller;
        await cancelled.CancelAsync();

        // Act
        Func<Task> read = () => view.ReadAsync(buffer.AsMemory(), caller.Token).AsTask();

        // Assert
        await read.ShouldThrow<TaskCanceledException>();
        body.Position.ShouldBe(0);
    }
}
