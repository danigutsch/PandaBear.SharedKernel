namespace SharedKernel.Repositories;

/// <summary>The version-enumeration request passed to the repository SDK process.</summary>
/// <param name="SchemaVersion">The version of the request format.</param>
/// <param name="Packages">The direct package scopes to enumerate.</param>
/// <param name="VersionLimit">The most distinct versions to retain from each feed set.</param>
internal sealed record NuGetVersionRequest(int SchemaVersion, NuGetVersionRequestPackage[] Packages, int VersionLimit);
