using System.Globalization;
using SharedKernel.ConfigurationFiles;

namespace SharedKernel.Packages;

/// <summary>
/// The consumer policy in <c>.config/sharedkernel-packages.json</c>: the declared packages per package manager and the
/// optional minimum release age. <see cref="Read"/> validates the whole file; errors name the file and the JSON path of
/// the problem.
/// </summary>
public sealed class PackagesConfiguration
{
    /// <summary>The configuration path relative to the repository root.</summary>
    public const string RelativePath = ".config/sharedkernel-packages.json";

    private PackagesConfiguration(DeclaredPackages packages, MinimumReleaseAge? minimumReleaseAge)
    {
        Packages = packages;
        MinimumReleaseAge = minimumReleaseAge;
    }

    /// <summary>Gets the declared packages, each with exactly one owning package manager.</summary>
    public DeclaredPackages Packages { get; }

    /// <summary>Gets the configured <c>minimumReleaseAgeDays</c>, or <see langword="null"/> when the file does not set it.</summary>
    public MinimumReleaseAge? MinimumReleaseAge { get; }

    /// <summary>Reads and validates the configuration under <paramref name="root"/> without following symbolic links.</summary>
    /// <param name="root">The repository root directory.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The validated configuration.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="root"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// The file is missing, larger than 1 MiB, or not a valid configuration of version 1, a manager ID or an entry is
    /// invalid, a package has more than one owner, or <c>minimumReleaseAgeDays</c> is outside 0 to 3650.
    /// </exception>
    public static async Task<PackagesConfiguration> Read(string root, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(root);
        var document = await JsonConfigurationFile.Read(
            root, RelativePath, PackagesConfigurationJsonContext.Default.PackagesConfigurationDocument, cancellationToken).ConfigureAwait(false);
        return Validate(document);
    }

    /// <summary>Validates the complete package declaration and its release policy.</summary>
    /// <exception cref="InvalidOperationException">A required field, package entry, ownership or policy setting is invalid.</exception>
    internal static PackagesConfiguration Validate(PackagesConfigurationDocument document)
    {
        if (document.Version != 1)
        {
            throw Invalid("$.version must be 1.");
        }

        MinimumReleaseAge? minimumAge = null;
        if (document.MinimumReleaseAgeDays is { } days)
        {
            var age = MinimumReleaseAge.FromDays(days);
            minimumAge = age.IsSuccess ? age.Value : throw Invalid($"$.minimumReleaseAgeDays must be from 0 to {MinimumReleaseAge.MaxDays}.");
        }

        Dictionary<PackageManagerKind, IReadOnlyList<PackageRequirement>> declared = [];
        foreach (var (id, entries) in document.Packages)
        {
            if (!PackageManagers.TryParse(id, out var manager))
            {
                throw Invalid($"$.packages.{id} is not a known package manager; the managers are: {string.Join(", ", PackageManagers.Ids)}.");
            }

            // RespectNullableAnnotations does not cover dictionary values or array elements.
            if (entries is null)
            {
                throw Invalid($"$.packages.{id} must be a list of package names, not null.");
            }

            List<PackageRequirement> requirements = [];
            for (var index = 0; index < entries.Length; index++)
            {
                requirements.Add(ParseEntry(id, index, entries[index]));
            }

            declared[manager.Value] = requirements;
        }

        var result = DeclaredPackages.Create(declared);
        if (result.IsFailure)
        {
            var messages = result.ErrorDetails?.ValidationErrors?.Values.SelectMany(static v => v) ?? [];
            throw Invalid("$.packages gives a package more than one owner:" + string.Concat(messages.Select(static m => "\n- " + m)));
        }

        return new PackagesConfiguration(result.Value, minimumAge);
    }

    private static PackageRequirement ParseEntry(string manager, int index, string? entry)
    {
        var path = string.Create(CultureInfo.InvariantCulture, $"$.packages.{manager}[{index}]");
        if (entry is null)
        {
            throw Invalid($"{path} must be a package name, not null.");
        }

        var separator = entry.IndexOf('=', StringComparison.Ordinal);
        var name = PackageName.Create(separator < 0 ? entry : entry[..separator]);
        if (!name.IsSuccess)
        {
            throw Invalid($"{path} must start with a valid package name.");
        }

        if (separator < 0)
        {
            return new PackageRequirement(name.Value);
        }

        var version = PackageVersion.Create(entry[(separator + 1)..]);
        return version.IsSuccess ? new PackageRequirement(name.Value, version.Value) : throw Invalid($"{path} must have a valid version after '='.");
    }

    private static InvalidOperationException Invalid(string message) => new($"{RelativePath}: {message}");
}
