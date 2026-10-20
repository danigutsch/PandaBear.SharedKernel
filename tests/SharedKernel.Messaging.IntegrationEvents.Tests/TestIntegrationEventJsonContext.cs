using System.Text.Json.Serialization;

namespace PandaBear.Messaging.IntegrationEvents.Tests;

[JsonSerializable(typeof(TestIntegrationEvent))]
[JsonSerializable(typeof(TestUpdatedIntegrationEvent))]
internal sealed partial class TestIntegrationEventJsonContext : JsonSerializerContext;
