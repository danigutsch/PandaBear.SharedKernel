using SharedKernel.Testing.Mediator;
using SharedKernel.Testing;

namespace SharedKernel.Mediator.Tests;

[Trait(SharedKernelTestTraitNames.CapabilityName, TestTraits.ReferenceDispatcherCapability)]
public sealed class ReferenceDispatcherTests
{
    [Fact]
    public async Task Reference_dispatcher_handles_request_response()
    {
        // Arrange
        List<string> events = [];
        var dispatcher = new ReferenceDispatcherBuilder()
            .AddRequestHandler(new ReferenceDispatcherTestTypes.LookupTourHandler(events))
            .Build();

        // Act
        var response = await dispatcher.Send(new ReferenceDispatcherTestTypes.LookupTour("vt-42"), CancellationToken.None);

        // Assert
        (response).ShouldBe("VT-42");
        (events).ShouldBe(["Handler:vt-42"]);
    }

    [Fact]
    public async Task Reference_dispatcher_handles_command_returning_unit()
    {
        // Arrange
        List<string> events = [];
        var dispatcher = new ReferenceDispatcherBuilder()
            .AddRequestHandler(new ReferenceDispatcherTestTypes.DeleteTourHandler(events))
            .Build();

        // Act
        var response = await dispatcher.Send(new ReferenceDispatcherTestTypes.DeleteTour(7), CancellationToken.None);

        // Assert
        (response).ShouldBe(Unit.Value);
        (events).ShouldBe(["Delete:7"]);
    }

    [Fact]
    public async Task Reference_dispatcher_throws_when_request_handler_is_missing()
    {
        // Arrange
        var dispatcher = new ReferenceDispatcherBuilder()
            .Build();

        // Act
        var exception = await ((Func<Task>)(() =>
            dispatcher.Send(new ReferenceDispatcherTestTypes.LookupTour("missing"), CancellationToken.None).AsTask())).ShouldThrow<NotSupportedException>();

        // Assert
        (exception.Message).ShouldContain(typeof(ReferenceDispatcherTestTypes.LookupTour).FullName!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reference_dispatcher_throws_when_request_handlers_are_ambiguous()
    {
        // Arrange
        List<string> events = [];
        var dispatcher = new ReferenceDispatcherBuilder()
            .AddRequestHandler(new ReferenceDispatcherTestTypes.LookupTourHandler(events))
            .AddRequestHandler(new ReferenceDispatcherTestTypes.LookupTourHandler(events))
            .Build();

        // Act
        var exception = await ((Func<Task>)(() =>
            dispatcher.Send(new ReferenceDispatcherTestTypes.LookupTour("duplicate"), CancellationToken.None).AsTask())).ShouldThrow<InvalidOperationException>();

        // Assert
        (exception.Message).ShouldContain("found 2 registered handlers", StringComparison.Ordinal);
        (exception.Message).ShouldContain(typeof(ReferenceDispatcherTestTypes.LookupTour).FullName!, StringComparison.Ordinal);
        (events).ShouldBeEmpty();
    }

    [Fact]
    public async Task Reference_dispatcher_executes_pipelines_in_deterministic_order()
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
        (response).ShouldBe("ORDER");
        (events).ShouldBe([
                "Validation:Before",
                "Observability:Before",
                "Handler:order",
                "Observability:After",
                "Validation:After",
            ]);
    }

    [Fact]
    public async Task Reference_dispatcher_ignores_notifications_without_handlers()
    {
        // Arrange
        List<string> events = [];
        var dispatcher = new ReferenceDispatcherBuilder()
            .Build();

        // Act
        await dispatcher.Publish(new ReferenceDispatcherTestTypes.DerivedNotification("tour"), CancellationToken.None);

        // Assert
        (events).ShouldBeEmpty();
    }

    [Fact]
    public async Task Reference_dispatcher_publishes_notifications_sequentially_with_exact_type_matching()
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
        (events).ShouldBe([
                "Derived-1:tour",
                "Derived-2:tour",
            ]);
    }

    [Fact]
    public async Task Reference_dispatcher_sends_streams()
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
        (items).ShouldBe(["Item-1", "Item-2", "Item-3"]);
    }

    [Fact]
    public async Task Reference_dispatcher_throws_when_stream_handler_is_missing()
    {
        // Arrange
        var dispatcher = new ReferenceDispatcherBuilder()
            .Build();

        // Act
        var exception = await ((Func<Task>)(async () =>
        {
            await using var enumerator =
                dispatcher.Send(new ReferenceDispatcherTestTypes.StreamTours(1), CancellationToken.None)
                    .GetAsyncEnumerator(CancellationToken.None);
            await enumerator.MoveNextAsync();
        })).ShouldThrow<NotSupportedException>();

        // Assert
        (exception.Message).ShouldContain(typeof(ReferenceDispatcherTestTypes.StreamTours).FullName!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reference_dispatcher_throws_when_stream_handlers_are_ambiguous()
    {
        // Arrange
        var dispatcher = new ReferenceDispatcherBuilder()
            .AddStreamHandler(new ReferenceDispatcherTestTypes.StreamToursHandler())
            .AddStreamHandler(new ReferenceDispatcherTestTypes.StreamToursHandler())
            .Build();

        // Act
        var exception = await ((Func<Task>)(async () =>
        {
            await using var enumerator =
                dispatcher.Send(new ReferenceDispatcherTestTypes.StreamTours(1), CancellationToken.None)
                    .GetAsyncEnumerator(CancellationToken.None);
            await enumerator.MoveNextAsync();
        })).ShouldThrow<InvalidOperationException>();

        // Assert
        (exception.Message).ShouldContain("found 2 registered handlers", StringComparison.Ordinal);
        (exception.Message).ShouldContain(typeof(ReferenceDispatcherTestTypes.StreamTours).FullName!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reference_dispatcher_executes_stream_pipelines_in_deterministic_order()
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
        (items).ShouldBe(["Item-1", "Item-2"]);
        (events).ShouldBe(["StreamValidation:Before", "StreamValidation:After"]);
    }
}
