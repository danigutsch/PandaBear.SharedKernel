namespace PandaBear.Blazor.Tests;

/// <summary>The stored record of the edit session tests.</summary>
/// <param name="Name">The name.</param>
/// <param name="Version">The concurrency version.</param>
internal sealed record EditSessionRecord(string Name, int Version);
