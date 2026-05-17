using SharedKernel.Mediator.Testing.ReferenceDispatcher;

namespace SharedKernel.Mediator.Tests;

[Trait(TestTraits.CapabilityName, TestTraits.ReferenceDispatcherCapability)]
public sealed class ReferenceDispatcherTests
{
    [Fact]
    public async Task Reference_Dispatcher_Handles_Request_Response()
    {
        // Arrange
        List<string> events = [];
        var dispatcher = new ReferenceDispatcherBuilder()
            .AddRequestHandler(new ReferenceDispatcherTestTypes.LookupTourHandler(events))
            .Build();

        // Act
        var response = await dispatcher.Send(new ReferenceDispatcherTestTypes.LookupTour("vt-42"), CancellationToken.None);

        // Assert
        Assert.Equal("VT-42", response);
        Assert.Equal(["Handler:vt-42"], events);
    }

    [Fact]
    public async Task Reference_Dispatcher_Handles_Command_Returning_Unit()
    {
        // Arrange
        List<string> events = [];
        var dispatcher = new ReferenceDispatcherBuilder()
            .AddRequestHandler(new ReferenceDispatcherTestTypes.DeleteTourHandler(events))
            .Build();

        // Act
        var response = await dispatcher.Send(new ReferenceDispatcherTestTypes.DeleteTour(7), CancellationToken.None);

        // Assert
        Assert.Equal(Unit.Value, response);
        Assert.Equal(["Delete:7"], events);
    }

    [Fact]
    public async Task Reference_Dispatcher_Throws_When_Request_Handler_Is_Missing()
    {
        // Arrange
        var dispatcher = new ReferenceDispatcherBuilder()
            .Build();

        // Act
        async Task Act()
        {
            await dispatcher.Send(new ReferenceDispatcherTestTypes.LookupTour("missing"), CancellationToken.None);
        }

        // Assert
        var exception = await Assert.ThrowsAsync<NotSupportedException>(Act);
        Assert.Contains(typeof(ReferenceDispatcherTestTypes.LookupTour).FullName!, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reference_Dispatcher_Throws_When_Request_Handlers_Are_Ambiguous()
    {
        // Arrange
        List<string> events = [];
        var dispatcher = new ReferenceDispatcherBuilder()
            .AddRequestHandler(new ReferenceDispatcherTestTypes.LookupTourHandler(events))
            .AddRequestHandler(new ReferenceDispatcherTestTypes.LookupTourHandler(events))
            .Build();

        // Act
        async Task Act()
        {
            await dispatcher.Send(new ReferenceDispatcherTestTypes.LookupTour("duplicate"), CancellationToken.None);
        }

        // Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(Act);
        Assert.Contains("found 2 registered handlers", exception.Message, StringComparison.Ordinal);
        Assert.Contains(typeof(ReferenceDispatcherTestTypes.LookupTour).FullName!, exception.Message, StringComparison.Ordinal);
        Assert.Empty(events);
    }

    [Fact]
    public async Task Reference_Dispatcher_Executes_Pipelines_In_Deterministic_Order()
    {
        // Arrange
        List<string> events = [];
        var dispatcher = new ReferenceDispatcherBuilder()
            .AddRequestHandler(new ReferenceDispatcherTestTypes.LookupTourHandler(events))
            .AddPipeline(new ReferenceDispatcherTestTypes.ObservabilityPipeline(events))
            .AddPipeline(new ReferenceDispatcherTestTypes.ValidationPipeline(events))
            .Build();

        // Act
        var response = await dispatcher.Send(new ReferenceDispatcherTestTypes.LookupTour("order"), CancellationToken.None);

        // Assert
        Assert.Equal("ORDER", response);
        Assert.Equal(
            [
                "Validation:Before",
                "Observability:Before",
                "Handler:order",
                "Observability:After",
                "Validation:After",
            ],
            events);
    }

    [Fact]
    public async Task Reference_Dispatcher_Ignores_Notifications_Without_Handlers()
    {
        // Arrange
        List<string> events = [];
        var dispatcher = new ReferenceDispatcherBuilder()
            .Build();

        // Act
        await dispatcher.Publish(new ReferenceDispatcherTestTypes.DerivedNotification("tour"), CancellationToken.None);

        // Assert
        Assert.Empty(events);
    }

    [Fact]
    public async Task Reference_Dispatcher_Publishes_Notifications_Sequentially_With_Exact_Type_Matching()
    {
        // Arrange
        List<string> events = [];
        ReferenceDispatcherTestTypes.BaseNotification notification = new ReferenceDispatcherTestTypes.DerivedNotification("tour");
        var dispatcher = new ReferenceDispatcherBuilder()
            .AddNotificationHandler(new ReferenceDispatcherTestTypes.BaseNotificationHandler(events))
            .AddNotificationHandler(new ReferenceDispatcherTestTypes.DerivedNotificationHandlerOne(events))
            .AddNotificationHandler(new ReferenceDispatcherTestTypes.DerivedNotificationHandlerTwo(events))
            .Build();

        // Act
        await dispatcher.Publish(notification, CancellationToken.None);

        // Assert
        Assert.Equal(
            [
                "Derived-1:tour",
                "Derived-2:tour",
            ],
            events);
    }

    [Fact]
    public async Task Reference_Dispatcher_Sends_Streams()
    {
        // Arrange
        List<string> items = [];
        var dispatcher = new ReferenceDispatcherBuilder()
            .AddStreamHandler(new ReferenceDispatcherTestTypes.StreamToursHandler())
            .Build();

        // Act
        await foreach (var item in dispatcher.Send(new ReferenceDispatcherTestTypes.StreamTours(3), CancellationToken.None))
        {
            items.Add(item);
        }

        // Assert
        Assert.Equal(["Item-1", "Item-2", "Item-3"], items);
    }

    [Fact]
    public async Task Reference_Dispatcher_Throws_When_Stream_Handler_Is_Missing()
    {
        // Arrange
        var dispatcher = new ReferenceDispatcherBuilder()
            .Build();

        // Act
        async Task Act()
        {
            await using var enumerator =
                dispatcher.Send(new ReferenceDispatcherTestTypes.StreamTours(1), CancellationToken.None)
                    .GetAsyncEnumerator(CancellationToken.None);
            await enumerator.MoveNextAsync();
        }

        // Assert
        var exception = await Assert.ThrowsAsync<NotSupportedException>(Act);
        Assert.Contains(typeof(ReferenceDispatcherTestTypes.StreamTours).FullName!, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reference_Dispatcher_Throws_When_Stream_Handlers_Are_Ambiguous()
    {
        // Arrange
        var dispatcher = new ReferenceDispatcherBuilder()
            .AddStreamHandler(new ReferenceDispatcherTestTypes.StreamToursHandler())
            .AddStreamHandler(new ReferenceDispatcherTestTypes.StreamToursHandler())
            .Build();

        // Act
        async Task Act()
        {
            await using var enumerator =
                dispatcher.Send(new ReferenceDispatcherTestTypes.StreamTours(1), CancellationToken.None)
                    .GetAsyncEnumerator(CancellationToken.None);
            await enumerator.MoveNextAsync();
        }

        // Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(Act);
        Assert.Contains("found 2 registered handlers", exception.Message, StringComparison.Ordinal);
        Assert.Contains(typeof(ReferenceDispatcherTestTypes.StreamTours).FullName!, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reference_Dispatcher_Executes_Stream_Pipelines_In_Deterministic_Order()
    {
        // Arrange
        List<string> events = [];
        List<string> items = [];
        var dispatcher = new ReferenceDispatcherBuilder()
            .AddStreamHandler(new ReferenceDispatcherTestTypes.StreamToursHandler())
            .AddStreamPipeline(new ReferenceDispatcherTestTypes.StreamValidationPipeline(events))
            .Build();

        // Act
        await foreach (var item in dispatcher.Send(new ReferenceDispatcherTestTypes.StreamTours(2), CancellationToken.None))
        {
            items.Add(item);
        }

        // Assert
        Assert.Equal(["Item-1", "Item-2"], items);
        Assert.Equal(["StreamValidation:Before", "StreamValidation:After"], events);
    }
}
