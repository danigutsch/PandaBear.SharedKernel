namespace PandaBear.Packages.Tool;

/// <summary>The consumer policy in <c>.config/sharedkernel-packages.json</c>.</summary>
/// <param name="Version">Format version; only <c>1</c> is supported.</param>
/// <param name="Packages">
/// The declared packages by package manager ID (<c>apt</c>, <c>cargo</c>, <c>npm</c>, <c>flatpak</c>, <c>snap</c>, <c>brew</c>). Each entry is a
/// package name, or <c>name=version</c> to require exactly that version.
/// </param>
/// <param name="MinimumReleaseAgeDays">
/// Optional. How many days a release must have been public before <c>updates</c> allows it; 0 to 3650. Absent means
/// that <c>updates</c> needs <c>--minimum-release-age-days</c>.
/// </param>
internal sealed record PackagesConfiguration(int Version, Dictionary<string, string[]> Packages, int? MinimumReleaseAgeDays = null);
