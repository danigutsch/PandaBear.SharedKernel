using SharedKernel.Domain;
using SharedKernel.Results;

namespace SharedKernel.Packages;

/// <summary>
/// The packages that should be installed, grouped by package manager. No package name appears twice, neither within
/// one manager nor under two managers, so one package has exactly one owner and managers cannot fight over it.
/// </summary>
public sealed class DeclaredPackages : ValueObject
{
    private readonly (PackageManagerKind Manager, PackageSet Packages)[] _groups;

    private DeclaredPackages((PackageManagerKind, PackageSet)[] groups) => _groups = groups;

    /// <summary>Gets the managers that have at least one declared package, in enum order.</summary>
    public IReadOnlyList<PackageManagerKind> Managers => [.. _groups.Select(static g => g.Manager)];

    /// <summary>Gets the requirements declared for <paramref name="manager"/>, ordered by name; empty when there are none.</summary>
    /// <param name="manager">The package manager.</param>
    public IReadOnlyList<PackageRequirement> For(PackageManagerKind manager) =>
        SetFor(manager).Requirements;

    /// <summary>Gets the immutable package set for <paramref name="manager"/>.</summary>
    /// <param name="manager">The package manager.</param>
    public PackageSet SetFor(PackageManagerKind manager) =>
        _groups.FirstOrDefault(group => group.Manager == manager).Packages ?? PackageSet.Empty;

    /// <summary>Creates the declaration, or fails with one error for every package that has more than one owner or a tap-qualified Homebrew name.</summary>
    /// <param name="declared">The requirements per manager. A manager with none is allowed and ignored.</param>
    /// <param name="field">The field name used in validation errors.</param>
    /// <returns>The declaration or a validation error that names each offending package and the managers involved.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="declared"/>, one of its lists, or a requirement is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A key is not a defined <see cref="PackageManagerKind"/>.</exception>
    public static Result<DeclaredPackages> Create(IReadOnlyDictionary<PackageManagerKind, IReadOnlyList<PackageRequirement>> declared, string field = "packages")
    {
        ArgumentNullException.ThrowIfNull(declared);

        var errors = new ValidationErrors();
        Dictionary<PackageName, List<PackageManagerKind>> owners = [];
        List<(PackageManagerKind Manager, PackageRequirement[] Requirements)> groups = [];
        foreach (var (manager, requirements) in declared.OrderBy(static pair => pair.Key))
        {
            ArgumentNullException.ThrowIfNull(requirements);
            if (!Enum.IsDefined(manager))
            {
                throw new ArgumentOutOfRangeException(nameof(declared), "A package manager is not a defined value.");
            }

            var snapshot = requirements.ToArray();
            if (snapshot.Length > 0)
            {
                groups.Add((manager, snapshot));
            }

            foreach (var requirement in snapshot)
            {
                ArgumentNullException.ThrowIfNull(requirement, nameof(declared));
                if (manager == PackageManagerKind.Brew && requirement.Name.Value.Contains('/', StringComparison.Ordinal))
                {
                    var message = $"{requirement.Name} is a tap-qualified Homebrew name; brew list prints only the short name, so declare it without the tap.";
                    errors.Add(Result.Invalid(detail: message, field: field, message: message));
                }

                if (!owners.TryGetValue(requirement.Name, out var list))
                {
                    owners[requirement.Name] = list = [];
                }

                list.Add(manager);
            }
        }

        foreach (var (name, managers) in owners.OrderBy(static pair => pair.Key.Value, StringComparer.Ordinal).Where(static pair => pair.Value.Count > 1))
        {
            var message = managers.Distinct().Count() == 1
                ? $"{name} is declared more than once for {PackageManagers.Id(managers[0])}."
                : $"{name} is declared for more than one package manager: {string.Join(", ", managers.Distinct().Select(PackageManagers.Id))}.";
            errors.Add(Result.Invalid(detail: message, field: field, message: message));
        }

        if (errors.HasErrors)
        {
            return errors.ToResult<DeclaredPackages>();
        }

        return Result.Ok(new DeclaredPackages([.. groups.Select(group => (group.Manager, PackageSet.Create(group.Requirements, field).Value))]));
    }

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        foreach (var (manager, packages) in _groups)
        {
            yield return manager;
            yield return packages;
        }
    }
}
