using System.Net;
using PandaBear.Testing;

namespace PandaBear.AI.Tests;

/// <summary>Tests that the LiteLLM image text generator enforces its maximum image size while reading the input stream and never disposes the caller's stream.</summary>
[Trait(SharedKernelTestTraitNames.CapabilityName, TestTraits.ImageTextGenerationCapability)]
public sealed class ImageInputLimitTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Generate_rejects_oversized_image_before_dispatch_without_disposing_input(bool seekable)
    {
        // Arrange
        const int limit = 5 * 1024 * 1024;
        using var image = new SeekableTrackingReadStream(new byte[limit + 100], seekable);
        using var handler = new TestHttpMessageHandler();
        using var client = new HttpClient(handler, disposeHandler: false);
        var generator = new LiteLlmImageTextGenerator(client, new LiteLlmImageTextGeneratorOptions
        {
            Endpoint = new Uri("https://litellm.example"),
            Model = "vision"
        });

        // Act
        Func<Task> generate = async () => await generator.GenerateImageText(
            new ImageTextGenerationRequest { Image = image, ContentType = "image/png", Language = "en" },
            TestContext.Current.CancellationToken);
        await generate.ShouldThrow<ImageTextGenerationException>();

        // Assert
        handler.Requests.Count.ShouldBe(0);
        image.BytesRead.ShouldBeLessThanOrEqualTo(limit + 1);
        image.IsDisposed.ShouldBeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Generate_accepts_exact_image_limit_and_preserves_input(bool seekable)
    {
        // Arrange
        const int limit = 5 * 1024 * 1024;
        using var image = new SeekableTrackingReadStream(new byte[limit], seekable);
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, "{\"choices\":[{\"message\":{\"content\":\"{\\\"altText\\\":\\\"Image\\\"}\"}}]}");
        using var client = new HttpClient(handler, disposeHandler: false);
        var generator = new LiteLlmImageTextGenerator(client, new LiteLlmImageTextGeneratorOptions
        {
            Endpoint = new Uri("https://litellm.example"),
            Model = "vision"
        });

        // Act
        var result = await generator.GenerateImageText(
            new ImageTextGenerationRequest { Image = image, ContentType = "image/png", Language = "en" },
            TestContext.Current.CancellationToken);

        // Assert
        result.AltText.ShouldBe("Image");
        handler.Requests.Count.ShouldBe(1);
        image.IsDisposed.ShouldBeFalse();
    }

    [Fact]
    public async Task Generate_preserves_cancellation_before_dispatch_and_leaves_source_open()
    {
        // Arrange
        using var image = new GatedCountingReadStream([1, 2, 3]);
        using var handler = new TestHttpMessageHandler();
        using var client = new HttpClient(handler, disposeHandler: false);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var generator = new LiteLlmImageTextGenerator(client, new LiteLlmImageTextGeneratorOptions
        {
            Endpoint = new Uri("https://litellm.example"),
            Model = "vision"
        });

        // Act
        var pending = generator.GenerateImageText(
            new ImageTextGenerationRequest { Image = image, ContentType = "image/png", Language = "en" }, cancellation.Token).AsTask();
        await image.ReadStarted.WaitAsync(TestContext.Current.CancellationToken);
        await cancellation.CancelAsync();
        Func<Task> wait = async () => await pending;
        var exception = await wait.ShouldThrowAssignableTo<OperationCanceledException>();

        // Assert
        exception.CancellationToken.ShouldBe(cancellation.Token);
        image.Position.ShouldBe(0);
        handler.Requests.Count.ShouldBe(0);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Generate_enforces_configured_limit_against_actual_bytes(bool seekable)
    {
        // Arrange
        using var image = new SeekableTrackingReadStream(new byte[100], seekable);
        using var handler = new TestHttpMessageHandler();
        using var client = new HttpClient(handler, disposeHandler: false);
        var generator = new LiteLlmImageTextGenerator(client, new LiteLlmImageTextGeneratorOptions
        {
            Endpoint = new Uri("https://litellm.example"),
            Model = "vision",
            MaxImageBytes = 3
        });

        // Act
        Func<Task> generate = async () => await generator.GenerateImageText(
            new ImageTextGenerationRequest { Image = image, ContentType = "image/png", Language = "en" }, TestContext.Current.CancellationToken);
        await generate.ShouldThrow<ImageTextGenerationException>();

        // Assert
        image.BytesRead.ShouldBe(4L);
        image.IsDisposed.ShouldBeFalse();
        handler.Requests.Count.ShouldBe(0);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(int.MaxValue)]
    public async Task Generate_reads_current_position_with_configured_limit_without_overflow(int limit)
    {
        // Arrange
        using var image = new SeekableTrackingReadStream([9, 9, 9, 1, 2, 3], true) { Position = 3 };
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, "{\"choices\":[{\"message\":{\"content\":\"{\\\"altText\\\":\\\"Image\\\"}\"}}]}");
        using var client = new HttpClient(handler, disposeHandler: false);
        var generator = new LiteLlmImageTextGenerator(client, new LiteLlmImageTextGeneratorOptions
        {
            Endpoint = new Uri("https://litellm.example"),
            Model = "vision",
            MaxImageBytes = limit
        });

        // Act
        var result = await generator.GenerateImageText(
            new ImageTextGenerationRequest { Image = image, ContentType = "image/png", Language = "en" }, TestContext.Current.CancellationToken);

        // Assert
        result.AltText.ShouldBe("Image");
        handler.LastRequest.ShouldNotBeNull().Body.ShouldNotBeNull().ShouldContain("data:image/png;base64,AQID", StringComparison.Ordinal);
        image.BytesRead.ShouldBe(3L);
        image.IsDisposed.ShouldBeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Generate_rejects_invalid_limit_before_reading_or_dispatch(int limit)
    {
        // Arrange
        using var image = new SeekableTrackingReadStream([1], false);
        using var handler = new TestHttpMessageHandler();
        using var client = new HttpClient(handler, disposeHandler: false);
        var generator = new LiteLlmImageTextGenerator(client, new LiteLlmImageTextGeneratorOptions
        {
            Endpoint = new Uri("https://litellm.example"),
            Model = "vision",
            MaxImageBytes = limit
        });

        // Act
        Func<Task> generate = async () => await generator.GenerateImageText(
            new ImageTextGenerationRequest { Image = image, ContentType = "image/png", Language = "en" }, TestContext.Current.CancellationToken);
        await generate.ShouldThrow<InvalidOperationException>();

        // Assert
        image.BytesRead.ShouldBe(0L);
        image.IsDisposed.ShouldBeFalse();
        handler.Requests.Count.ShouldBe(0);
    }
}
