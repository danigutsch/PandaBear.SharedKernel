using SharedKernel.AI;
using SharedKernel.Testing.AI;

namespace SharedKernel.Testing.Tests;

/// <summary>Verifies how the configured AI fake records and answers image text requests.</summary>
public sealed class TestImageTextGeneratorTests
{
    [Fact]
    public async Task Returns_the_configured_result_without_reading_the_image()
    {
        // Arrange
        using var image = new ThrowingReadStream(new IOException("Unexpected read"));
        var request = new ImageTextGenerationRequest { Image = image, ContentType = "image/png", Language = "en" };
        var expected = new ImageTextGenerationResult("A mountain", null);
        var generator = new TestImageTextGenerator(expected);

        // Act
        var result = await generator.GenerateImageText(request, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeSameAs(expected);
        generator.LastRequest.ShouldBeSameAs(request);
    }

    [Fact]
    public async Task Records_the_request_before_throwing_the_configured_failure()
    {
        // Arrange
        var request = new ImageTextGenerationRequest { Image = Stream.Null, ContentType = "image/png", Language = "en" };
        var expected = new InvalidOperationException("Generation unavailable");
        var callbackCalled = false;
        var generator = new TestImageTextGenerator(new ImageTextGenerationResult("A mountain", null))
        {
            Exception = expected,
            BeforeResult = _ =>
            {
                callbackCalled = true;
                return ValueTask.CompletedTask;
            },
        };

        // Act
        Func<Task> generate = async () => await generator.GenerateImageText(request, TestContext.Current.CancellationToken);
        var failure = await generate.ShouldThrow<InvalidOperationException>();

        // Assert
        failure.ShouldBeSameAs(expected);
        callbackCalled.ShouldBeFalse();
        generator.LastRequest.ShouldBeSameAs(request);
    }

    [Fact]
    public async Task Rejects_a_cancelled_request_before_recording_it()
    {
        // Arrange
        var request = new ImageTextGenerationRequest { Image = Stream.Null, ContentType = "image/png", Language = "en" };
        var callbackCalled = false;
        var generator = new TestImageTextGenerator(new ImageTextGenerationResult("A mountain", null))
        {
            BeforeResult = _ =>
            {
                callbackCalled = true;
                return ValueTask.CompletedTask;
            },
        };
        var cancellation = new CancellationToken(canceled: true);

        // Act
        Func<Task> generate = async () => await generator.GenerateImageText(request, cancellation);
        var failure = await generate.ShouldThrow<OperationCanceledException>();

        // Assert
        failure.CancellationToken.ShouldBe(cancellation);
        callbackCalled.ShouldBeFalse();
        generator.LastRequest.ShouldBeNull();
    }

    [Fact]
    public async Task Awaits_the_callback_before_returning_the_fixed_result()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var request = new ImageTextGenerationRequest { Image = Stream.Null, ContentType = "image/png", Language = "en" };
        var expected = new ImageTextGenerationResult("A mountain", null);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken? observedToken = null;
        var generator = new TestImageTextGenerator(expected)
        {
            BeforeResult = token =>
            {
                observedToken = token;
                return new ValueTask(release.Task.WaitAsync(token));
            },
        };

        // Act
        var generation = generator.GenerateImageText(request, ct).AsTask();
        var wasPending = !generation.IsCompleted;
        var recordedWhilePending = generator.LastRequest;
        release.SetResult();
        var result = await generation;

        // Assert
        wasPending.ShouldBeTrue();
        observedToken.ShouldBe(ct);
        recordedWhilePending.ShouldBeSameAs(request);
        result.ShouldBeSameAs(expected);
    }

    [Fact]
    public async Task Preserves_the_recorded_request_when_the_callback_cancels_the_invocation()
    {
        // Arrange
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var request = new ImageTextGenerationRequest { Image = Stream.Null, ContentType = "image/png", Language = "en" };
        var generator = new TestImageTextGenerator(new ImageTextGenerationResult("A mountain", null))
        {
            BeforeResult = _ => new ValueTask(cancellation.CancelAsync()),
        };

        // Act
        var generation = generator.GenerateImageText(request, cancellation.Token).AsTask();
        Func<Task> generate = () => generation;
        var failure = await generate.ShouldThrow<TaskCanceledException>();

        // Assert
        failure.CancellationToken.ShouldBe(cancellation.Token);
        generation.IsCanceled.ShouldBeTrue();
        generator.LastRequest.ShouldBeSameAs(request);
    }

    [Fact]
    public async Task Rejects_missing_result_and_request()
    {
        // Arrange
        var generator = new TestImageTextGenerator(new ImageTextGenerationResult("A mountain", null));

        // Act
        Action missingResult = () => _ = new TestImageTextGenerator(null!);
        Func<Task> missingRequest = async () => await generator.GenerateImageText(null!, TestContext.Current.CancellationToken);

        // Assert
        missingResult.ShouldThrow<ArgumentNullException>();
        await missingRequest.ShouldThrow<ArgumentNullException>();
    }
}
