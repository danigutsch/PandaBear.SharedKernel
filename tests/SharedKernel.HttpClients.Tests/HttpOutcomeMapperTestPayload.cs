namespace PandaBear.HttpClients.Tests;

/// <summary>Minimal JSON payload with a single name, deserialized by the HTTP outcome mapper tests.</summary>
/// <param name="Name">The payload name.</param>
internal sealed record HttpOutcomeMapperTestPayload(string Name);
