using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace PandaBear.Messaging.IntegrationEvents.Tests;

/// <summary>
/// A service collection wrapper for testing how AddIntegrationEventContract handles existing and keyed JSON metadata registrations.
/// </summary>
internal sealed class IntegrationEventContractRegistrationScenario
{
    private readonly ServiceCollection services = [];

    /// <summary>
    /// Gets how many unkeyed TestIntegrationEvent JSON metadata registrations are in the collection.
    /// </summary>
    public int UnkeyedRegistrationCount => services.Count(IsUnkeyedContractMetadata);

    /// <summary>
    /// Registers the JSON metadata as an unkeyed singleton, as if added before the contract.
    /// </summary>
    public void AddExisting(JsonTypeInfo<TestIntegrationEvent> jsonTypeInfo) =>
        services.AddSingleton(jsonTypeInfo);

    /// <summary>
    /// Registers the TestIntegrationEvent contract under its event type with the given JSON metadata.
    /// </summary>
    public void Register(JsonTypeInfo<TestIntegrationEvent> jsonTypeInfo) =>
        services.AddIntegrationEventContract(TestIntegrationEvent.EventType, jsonTypeInfo);

    /// <summary>
    /// Registers the JSON metadata as a keyed singleton under the given key.
    /// </summary>
    public void RegisterKeyed(string key, JsonTypeInfo<TestIntegrationEvent> jsonTypeInfo) =>
        services.AddKeyedSingleton(key, jsonTypeInfo);

    /// <summary>
    /// Returns the single unkeyed JSON metadata instance, failing when there is not exactly one.
    /// </summary>
    public JsonTypeInfo<TestIntegrationEvent> GetUnkeyedMetadata()
    {
        var descriptor = services.ShouldHaveSingleItem(IsUnkeyedContractMetadata);
        return descriptor.ImplementationInstance.ShouldBeOfType<JsonTypeInfo<TestIntegrationEvent>>();
    }

    private static bool IsUnkeyedContractMetadata(ServiceDescriptor descriptor) =>
        !descriptor.IsKeyedService
        && descriptor.ServiceType == typeof(JsonTypeInfo<TestIntegrationEvent>);
}
