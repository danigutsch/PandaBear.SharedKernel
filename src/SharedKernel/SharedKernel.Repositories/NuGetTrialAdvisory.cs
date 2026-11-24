namespace PandaBear.Repositories;

/// <summary>Identifies an advisory reported by the SDK under the evaluated NuGet audit policy.</summary>
/// <param name="Code">NuGet audit severity code.</param>
/// <param name="Package">Affected package identifier.</param>
/// <param name="Version">Affected resolved version.</param>
/// <param name="Advisory">The advisory identifier reported as a URL without query or fragment.</param>
/// <param name="Project">Repository-relative project path, when available.</param>
internal sealed record NuGetTrialAdvisory(string Code, string Package, string Version, string Advisory, string? Project);
