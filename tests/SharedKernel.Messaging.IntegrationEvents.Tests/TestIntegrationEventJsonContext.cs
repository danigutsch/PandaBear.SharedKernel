using System.Text.Json.Serialization;

namespace PandaBear.Messaging.IntegrationEvents.Tests;

/// <summary>Source-generated JSON serializer context covering the created and updated test integration events.</summary>
[JsonSerializable(typeof(TestIntegrationEvent))]
[JsonSerializable(typeof(TestUpdatedIntegrationEvent))]
internal sealed partial class TestIntegrationEventJsonContext : JsonSerializerContext;
