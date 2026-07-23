using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace SharedKernel.Messaging.IntegrationEvents.Tests;

internal sealed class IntegrationEventContractRegistrationScenario
{
    private readonly ServiceCollection services = [];

    public int UnkeyedRegistrationCount => services.Count(IsUnkeyedContractMetadata);

    public void AddExisting(JsonTypeInfo<TestIntegrationEvent> jsonTypeInfo) =>
        services.AddSingleton(jsonTypeInfo);

    public void Register(JsonTypeInfo<TestIntegrationEvent> jsonTypeInfo) =>
        services.AddIntegrationEventContract(TestIntegrationEvent.EventType, jsonTypeInfo);

    public void RegisterKeyed(string key, JsonTypeInfo<TestIntegrationEvent> jsonTypeInfo) =>
        services.AddKeyedSingleton(key, jsonTypeInfo);

    public JsonTypeInfo<TestIntegrationEvent> GetUnkeyedMetadata()
    {
        var descriptor = services.ShouldHaveSingleItem(IsUnkeyedContractMetadata);
        return descriptor.ImplementationInstance.ShouldBeOfType<JsonTypeInfo<TestIntegrationEvent>>();
    }

    private static bool IsUnkeyedContractMetadata(ServiceDescriptor descriptor) =>
        !descriptor.IsKeyedService
        && descriptor.ServiceType == typeof(JsonTypeInfo<TestIntegrationEvent>);
}
