namespace PandaBear.AI.Tests;

/// <summary>Exercises the LiteLLM transport's deadline and cancellation behavior.</summary>
[Trait(Testing.SharedKernelTestTraitNames.CapabilityName, TestTraits.ImageTextGenerationCapability)]
public sealed class LiteLlmImageTextGeneratorTransportTests
{
    private static readonly LiteLlmImageTextGeneratorOptions Options = new()
    {
        Endpoint = new Uri("https://litellm.example"),
        Model = "local/vision"
    };

    [Fact]
    public async Task Generate_image_text_reports_an_expired_HttpClient_deadline_as_provider_failure()
    {
        // Arrange
        const string context = "synthetic-editorial-marker";
        using var handler = new GatedHttpMessageHandler();
        using var client = new HttpClient(handler, disposeHandler: false) { Timeout = TimeSpan.FromMilliseconds(250) };
        var generator = new LiteLlmImageTextGenerator(client, Options);
        var caller = TestContext.Current.CancellationToken;
        var request = new ImageTextGenerationRequest
        {
            Image = Stream.Null,
            ContentType = "image/png",
            Language = "pt-BR",
            Context = context
        };

        // Act
        var action = () => generator.GenerateImageText(request, caller).AsTask();

        // Assert
        var exception = await action.ShouldThrow<ImageTextGenerationException>();
        exception.Message.ShouldBe("LiteLLM request timed out.");
        var cancellation = exception.InnerException.ShouldBeAssignableTo<OperationCanceledException>();
        cancellation.InnerException.ShouldBeOfType<TimeoutException>();
        exception.ToString().ShouldNotContain(context, StringComparison.Ordinal);
        caller.IsCancellationRequested.ShouldBeFalse();
        handler.Requests.Count.ShouldBe(1);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Generate_image_text_preserves_cancellation_without_a_transport_deadline(bool cancelCaller)
    {
        // Arrange
        using var handler = new GatedHttpMessageHandler();
        using var client = new HttpClient(handler, disposeHandler: false) { Timeout = Timeout.InfiniteTimeSpan };
        var generator = new LiteLlmImageTextGenerator(client, Options);
        var testCancellation = TestContext.Current.CancellationToken;
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(testCancellation);
        var request = new ImageTextGenerationRequest { Image = Stream.Null, ContentType = "image/png", Language = "en-US" };

        // Act
        var pending = generator.GenerateImageText(request, caller.Token).AsTask();
        await handler.WaitForRequests(1, testCancellation);
        if (cancelCaller)
        {
            await caller.CancelAsync();
        }
        else
        {
            client.CancelPendingRequests();
        }

        Func<Task> action = () => pending;

        // Assert
        var exception = await action.ShouldThrowAssignableTo<OperationCanceledException>();
        caller.IsCancellationRequested.ShouldBe(cancelCaller);
        if (cancelCaller)
        {
            exception.CancellationToken.ShouldBe(caller.Token);
        }

        handler.Requests.Count.ShouldBe(1);
    }
}
