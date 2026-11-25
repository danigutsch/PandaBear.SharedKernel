namespace SharedKernel.Repositories;

/// <summary>Records whether an owner's versions were read from every eligible source.</summary>
/// <param name="Package">The direct package ID.</param>
/// <param name="Status">The completeness of the enumeration.</param>
/// <param name="Versions">The candidate versions in ascending NuGet order when the evidence is complete.</param>
internal sealed record NuGetVersionSet(string Package, string Status, string[] Versions);
