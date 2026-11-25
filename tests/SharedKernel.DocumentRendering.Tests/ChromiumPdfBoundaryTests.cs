using Microsoft.Playwright;
using PandaBear.DocumentRendering.Pdf.Chromium;
using PandaBear.Testing;

namespace PandaBear.DocumentRendering.Tests;

/// <summary>Verifies error precedence and the bounded browser protocol shape.</summary>
[Trait(SharedKernelTestTraitNames.CapabilityName, TestTraits.DocumentRenderingCapability)]
public sealed class ChromiumPdfBoundaryTests
{
    [Fact]
    public async Task A_context_close_failure_preserves_caller_cancellation()
    {
        // Arrange
        using var caller = new CancellationTokenSource();
        await caller.CancelAsync();
        Func<Task> render = () => ChromiumDocumentRenderer.PrintAndClose(
            () => Task.FromCanceled<ReadOnlyMemory<byte>>(caller.Token),
            () => Task.FromException(new PlaywrightException("Browser context close failed.")));

        // Act
        var failure = await render.ShouldThrow<TaskCanceledException>();

        // Assert
        failure.CancellationToken.ShouldBe(caller.Token);
    }

    [Fact]
    public async Task A_context_close_failure_preserves_the_transfer_rejection()
    {
        // Arrange
        var transferFailure = new InvalidDataException("PDF exceeded its byte limit.");
        Func<Task> render = () => ChromiumDocumentRenderer.PrintAndClose(
            () => Task.FromException<ReadOnlyMemory<byte>>(transferFailure),
            () => Task.FromException(new TimeoutException("Browser context close did not finish.")));

        // Act
        var failure = await render.ShouldThrow<InvalidDataException>();

        // Assert
        ReferenceEquals(failure, transferFailure).ShouldBeTrue();
    }

    [Fact]
    public async Task A_context_close_failure_rejects_a_successfully_printed_document()
    {
        // Arrange
        var closeFailure = new PlaywrightException("Browser context close failed.");
        Func<Task> render = () => ChromiumDocumentRenderer.PrintAndClose(
            () => Task.FromResult<ReadOnlyMemory<byte>>(new byte[1]),
            () => Task.FromException(closeFailure));

        // Act
        var failure = await render.ShouldThrow<PlaywrightException>();

        // Assert
        ReferenceEquals(failure, closeFailure).ShouldBeTrue();
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("{\"stream\":42}")]
    [InlineData("{\"stream\":\"\"}")]
    [InlineData("{\"stream\":\"fixture\",\"data\":null}")]
    [InlineData("{\"stream\":\"fixture\",\"data\":\"AAAA\"}")]
    public async Task Rejects_malformed_developer_tools_stream_headers(string reply)
    {
        // Arrange
        await using var session = new ChromiumProtocolReplyStub(reply);
        Func<Task> print = () => ChromiumPdfTransfer.Print(session, 8, TestContext.Current.CancellationToken);

        // Act
        var failure = await print.ShouldThrow<InvalidDataException>();

        // Assert
        failure.Message.ShouldContain("PDF stream", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("{\"data\":4,\"eof\":true}")]
    [InlineData("{\"data\":\"\"}")]
    [InlineData("{\"data\":\"\",\"eof\":\"true\"}")]
    [InlineData("{\"data\":\"\",\"eof\":false}")]
    [InlineData("{\"data\":\"YQ==\",\"eof\":true,\"base64Encoded\":false}")]
    [InlineData("{\"data\":\"YQ==\",\"eof\":true}")]
    [InlineData("{\"data\":\"!!!!\",\"eof\":true,\"base64Encoded\":true}")]
    public async Task Rejects_malformed_developer_tools_stream_chunks(string reply)
    {
        // Arrange
        await using var session = new ChromiumProtocolReplyStub("{\"stream\":\"fixture\"}", reply);
        Func<Task> print = () => ChromiumPdfTransfer.Print(session, 8, TestContext.Current.CancellationToken);

        // Act
        var failure = await print.ShouldThrow<InvalidDataException>();

        // Assert
        failure.Message.ShouldContain("PDF chunk", StringComparison.Ordinal);
    }
}
