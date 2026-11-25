namespace SharedKernel.Repositories;

/// <summary>The sanitized, bounded enumeration returned from the managed SDK process.</summary>
/// <param name="SchemaVersion">The version of the response format.</param>
/// <param name="Packages">The outcomes in the order requested.</param>
internal sealed record NuGetVersionResponse(int SchemaVersion, NuGetVersionSet[] Packages);
