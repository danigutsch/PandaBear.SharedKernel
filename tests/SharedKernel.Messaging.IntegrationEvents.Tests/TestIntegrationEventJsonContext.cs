using System.Text.Json.Serialization;

namespace SharedKernel.Messaging.IntegrationEvents.Tests;

[JsonSerializable(typeof(TestIntegrationEvent))]
internal sealed partial class TestIntegrationEventJsonContext : JsonSerializerContext;
