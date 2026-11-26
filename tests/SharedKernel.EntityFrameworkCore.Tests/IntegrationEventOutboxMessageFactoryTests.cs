using System.Diagnostics;
using System.Text.Json;
using SharedKernel.Messaging;
using SharedKernel.Messaging.IntegrationEvents;
using SharedKernel.Messaging.IntegrationEvents.EntityFrameworkCore;

namespace SharedKernel.EntityFrameworkCore.Tests;

/// <summary>
/// Tests that outbox rows record the payload, encoding, and content type the serializer reports.
/// </summary>
public sealed class IntegrationEventOutboxMessageFactoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Workflow_metadata_preserves_the_payload_and_existing_trace_attributes(bool useBinarySerializer)
    {
        // Arrange
        IIntegrationEventSerializer serializer = useBinarySerializer
            ? new Base64TestIntegrationEventSerializer()
            : new ComposedMessagingTestIntegrationEventSerializer();
        var integrationEvent = new ConsumerTestIntegrationEvent(Guid.CreateVersion7(), Now, Guid.CreateVersion7(), "code", "name");
        var workflowId = Guid.CreateVersion7();
        using var activity = new Activity("workflow-outbox") { TraceStateString = "example=value" };
        activity.Start();
        var uncorrelated = IntegrationEventOutboxMessageFactory.Create(integrationEvent, serializer, Now);

        // Act
        var correlated = IntegrationEventOutboxMessageFactory.Create(integrationEvent, serializer, Now, workflowId);

        // Assert
        correlated.Payload.ShouldBe(uncorrelated.Payload);
        correlated.PayloadEncoding.ShouldBe(uncorrelated.PayloadEncoding);
        correlated.DataContentType.ShouldBe(uncorrelated.DataContentType);
        correlated.EventId.ShouldBe(uncorrelated.EventId);
        correlated.WorkflowCorrelationId.ShouldBe(workflowId);
        uncorrelated.WorkflowCorrelationId.ShouldBeNull();
        using var attributes = JsonDocument.Parse(correlated.ExtensionAttributesJson.ShouldNotBeNull());
        attributes.RootElement.GetProperty("workflowid").GetGuid().ShouldBe(workflowId);
        attributes.RootElement.GetProperty("traceparent").GetString().ShouldBe(activity.Id);
        attributes.RootElement.GetProperty("tracestate").GetString().ShouldBe("example=value");
    }

    [Fact]
    public void Create_withdefaultserializer_recordsjsonpayload()
    {
        // Arrange
        var integrationEvent = new ConsumerTestIntegrationEvent(Guid.CreateVersion7(), Now, Guid.CreateVersion7(), "code", "name");

        // Act
        var message = IntegrationEventOutboxMessageFactory.Create(integrationEvent, new ComposedMessagingTestIntegrationEventSerializer(), Now);

        // Assert
        message.Payload.ShouldBe("{}");
        message.PayloadEncoding.ShouldBe(EventPayloadEncoding.Json);
        message.DataContentType.ShouldBe("application/json");
    }

    [Fact]
    public void Create_withbinaryserializer_recordsthereportedencodingandcontenttype()
    {
        // Arrange
        var integrationEvent = new ConsumerTestIntegrationEvent(Guid.CreateVersion7(), Now, Guid.CreateVersion7(), "code", "name");

        // Act
        var message = IntegrationEventOutboxMessageFactory.Create(integrationEvent, new Base64TestIntegrationEventSerializer(), Now);

        // Assert
        message.Payload.ShouldBe("CgRBbHBz");
        message.PayloadEncoding.ShouldBe(EventPayloadEncoding.Base64);
        message.DataContentType.ShouldBe("application/protobuf");
    }
}
