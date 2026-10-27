using Google.Protobuf;
using Microsoft.Extensions.DependencyInjection;

namespace PandaBear.Messaging.Protobuf.Tests;

/// <summary>Tests delivery of Protobuf envelopes to handlers and the hand-off of other envelopes to the publisher registered before.</summary>
public sealed class ProtobufIntegrationEventConsumerTests
{
    private static readonly DateTimeOffset Occurred = DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_000_123);

    [Fact]
    public async Task Publish_withprotobufenvelope_deliversthedeserializedeventtothehandler()
    {
        // Arrange
        CapturingTourCreatedHandler.Handled.Clear();
        var integrationEvent = TourCreated.Create(Guid.NewGuid(), Occurred, "Alps");
        var services = new ServiceCollection().AddProtobufIntegrationEventConsumer<TourCreated, CapturingTourCreatedHandler>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        // Act
        await scope.ServiceProvider.GetRequiredService<IEventEnvelopePublisher>()
            .Publish(ProtobufEnvelopes.ForProtobuf(integrationEvent), TestContext.Current.CancellationToken);

        // Assert
        CapturingTourCreatedHandler.Handled.ShouldBe([integrationEvent]);
    }

    [Fact]
    public async Task Publish_withjsonenvelope_passesittothepublisherregisteredbefore()
    {
        // Arrange
        var inner = new RecordingEnvelopePublisher();
        var services = new ServiceCollection().AddSingleton<IEventEnvelopePublisher>(inner);
        services.AddProtobufIntegrationEventConsumer<TourCreated, CapturingTourCreatedHandler>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var json = ProtobufEnvelopes.Create("other.created", "{}", EventPayloadEncoding.Json, "application/json");

        // Act
        await scope.ServiceProvider.GetRequiredService<IEventEnvelopePublisher>().Publish(json, TestContext.Current.CancellationToken);

        // Assert
        inner.Received.ShouldBe([json]);
    }

    [Fact]
    public async Task Publish_withjsonenvelopeandascopedtypeinner_resolvestheinnerfromthecontainer()
    {
        // Arrange
        StaticRecordingEnvelopePublisher.Received.Clear();
        var services = new ServiceCollection().AddScoped<IEventEnvelopePublisher, StaticRecordingEnvelopePublisher>();
        services.AddProtobufIntegrationEventConsumer<TourCreated, CapturingTourCreatedHandler>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var json = ProtobufEnvelopes.Create("other.created", "{}", EventPayloadEncoding.Json, "application/json");

        // Act
        await scope.ServiceProvider.GetRequiredService<IEventEnvelopePublisher>().Publish(json, TestContext.Current.CancellationToken);

        // Assert
        StaticRecordingEnvelopePublisher.Received.ShouldBe([json]);
    }

    [Fact]
    public async Task Publish_withjsonenvelopeandnopublisherregisteredbefore_failswithanactionableerror()
    {
        // Arrange
        var services = new ServiceCollection().AddProtobufIntegrationEventConsumer<TourCreated, CapturingTourCreatedHandler>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IEventEnvelopePublisher>();
        var json = ProtobufEnvelopes.Create("other.created", "{}", EventPayloadEncoding.Json, "application/json");
        Func<Task> act = async () => await publisher.Publish(json, TestContext.Current.CancellationToken);

        // Act
        var exception = await act.ShouldThrow<NotSupportedException>();

        // Assert
        exception.Message.ShouldContain("other.created", StringComparison.Ordinal);
        exception.Message.ShouldContain("AddGeneratedIntegrationEvents", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Publish_withunregisteredprotobufeventtype_failswithanactionableerror()
    {
        // Arrange
        var services = new ServiceCollection().AddProtobufIntegrationEventConsumer<TourCreated, CapturingTourCreatedHandler>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IEventEnvelopePublisher>();
        var unknown = ProtobufEnvelopes.Create("unknown.created", "CgA=", EventPayloadEncoding.Base64, ProtobufContentTypes.Payload);
        Func<Task> act = async () => await publisher.Publish(unknown, TestContext.Current.CancellationToken);

        // Act
        var exception = await act.ShouldThrow<NotSupportedException>();

        // Assert
        exception.Message.ShouldContain("unknown.created", StringComparison.Ordinal);
        exception.Message.ShouldContain("AddProtobufIntegrationEventConsumer", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Publish_withcorruptprotobufpayload_throwsinvalidprotocolbufferexception()
    {
        // Arrange
        var services = new ServiceCollection().AddProtobufIntegrationEventConsumer<TourCreated, CapturingTourCreatedHandler>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IEventEnvelopePublisher>();
        var corrupt = ProtobufEnvelopes.Create(TourCreated.EventType, Convert.ToBase64String([0x0A, 0x7F]), EventPayloadEncoding.Base64, ProtobufContentTypes.Payload);
        Func<Task> act = async () => await publisher.Publish(corrupt, TestContext.Current.CancellationToken);

        // Act, Assert
        await act.ShouldThrow<InvalidProtocolBufferException>();
    }

    [Fact]
    public void AddProtobufIntegrationEventConsumer_twiceforoneeventtype_throwsinvalidoperationexception()
    {
        // Arrange
        var services = new ServiceCollection().AddProtobufIntegrationEventConsumer<TourCreated, CapturingTourCreatedHandler>();
        Func<object?> act = () => services.AddProtobufIntegrationEventConsumer<TourCreated, CapturingTourCreatedHandler>();

        // Act
        var exception = act.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldContain("test.tour.created", StringComparison.Ordinal);
    }

    [Fact]
    public void AddProtobufIntegrationEventConsumer_withnullservices_throwsargumentnullexception()
    {
        // Arrange
        Func<object?> act = () => ProtobufIntegrationEventConsumerServiceCollectionExtensions
            .AddProtobufIntegrationEventConsumer<TourCreated, CapturingTourCreatedHandler>(null!);

        // Act, Assert
        act.ShouldThrow<ArgumentNullException>();
    }
}
