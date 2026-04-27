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
    public async Task Reference_Dispatcher_Creates_Streams()
    {
        // Arrange
        List<string> items = [];
        var dispatcher = new ReferenceDispatcherBuilder()
            .AddStreamHandler(new ReferenceDispatcherTestTypes.StreamToursHandler())
            .Build();

        // Act
        await foreach (var item in dispatcher.CreateStream(new ReferenceDispatcherTestTypes.StreamTours(3), CancellationToken.None))
        {
            items.Add(item);
        }

        // Assert
        Assert.Equal(["Item-1", "Item-2", "Item-3"], items);
    }
}
