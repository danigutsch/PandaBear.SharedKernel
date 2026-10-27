namespace PandaBear.OpenApi.Tests;

/// <summary>
/// Represents a non-form request body for OpenAPI transformer tests.
/// </summary>
/// <param name="Name">The name carried by the request.</param>
internal sealed record TestJsonRequest(string Name);
