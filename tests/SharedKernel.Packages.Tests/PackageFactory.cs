namespace PandaBear.Packages.Tests;

/// <summary>Builds valid package values for tests.</summary>
internal static class PackageFactory
{
    /// <summary>Creates a valid package name.</summary>
    public static PackageName Name(string value) => PackageName.Create(value).Value;

    /// <summary>Creates a valid package version.</summary>
    public static PackageVersion Version(string value) => PackageVersion.Create(value).Value;

    /// <summary>Creates an installed package.</summary>
    public static InstalledPackage Installed(string name, string version) => new(Name(name), Version(version));

    /// <summary>Creates a requirement, with an exact version when <paramref name="version"/> is given.</summary>
    public static PackageRequirement Require(string name, string? version = null) => new(Name(name), version is null ? null : Version(version));

    /// <summary>Creates an update candidate.</summary>
    public static UpdateCandidate Candidate(string name, string installed, string candidate, DateTimeOffset releasedAt) =>
        new(Name(name), Version(installed), Version(candidate), releasedAt);

    /// <summary>Creates a minimum release age of whole days.</summary>
    public static MinimumReleaseAge Age(int days) => MinimumReleaseAge.FromDays(days).Value;

    /// <summary>Creates a high-severity advisory that affects the given versions.</summary>
    public static Advisory AdvisoryFor(string package, string id, params string[] affected) =>
        Advisory.Create(id, Name(package), AdvisorySeverity.High, affected.Select(Version)).Value;

    /// <summary>Builds a declaration input; an entry is a name or <c>name=version</c>.</summary>
    public static Dictionary<PackageManagerKind, IReadOnlyList<PackageRequirement>> Declare(params (PackageManagerKind Manager, string[] Entries)[] groups) =>
        groups.ToDictionary(static g => g.Manager, static g => (IReadOnlyList<PackageRequirement>)[.. g.Entries.Select(Entry)]);

    /// <summary>Parses a <c>name</c> or <c>name=version</c> entry into a requirement.</summary>
    public static PackageRequirement Entry(string text)
    {
        var parts = text.Split('=');
        return Require(parts[0], parts.Length > 1 ? parts[1] : null);
    }

    /// <summary>Creates a reader over the UTF-8 bytes of <paramref name="text"/>.</summary>
    public static StreamReader Reader(string text) => new(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(text)));
}
