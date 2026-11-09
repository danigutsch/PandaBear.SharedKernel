using PandaBear.BuildingBlocks;
using PandaBear.Results;

namespace PandaBear.Packages;

/// <summary>
/// The packages that should be installed, grouped by package manager. No package name appears twice, neither within
/// one manager nor under two managers, so one package has exactly one owner and managers cannot fight over it.
/// </summary>
public sealed class DeclaredPackages : ValueObject
{
    private readonly (PackageManagerKind Manager, PackageRequirement[] Requirements)[] _groups;

    private DeclaredPackages((PackageManagerKind, PackageRequirement[])[] groups) => _groups = groups;

    /// <summary>Gets the managers that have at least one declared package, in enum order.</summary>
    public IReadOnlyList<PackageManagerKind> Managers => [.. _groups.Select(static g => g.Manager)];

    /// <summary>Gets the requirements declared for <paramref name="manager"/>, ordered by name; empty when there are none.</summary>
    /// <param name="manager">The package manager.</param>
    public IReadOnlyList<PackageRequirement> For(PackageManagerKind manager) =>
        _groups.FirstOrDefault(g => g.Manager == manager).Requirements ?? [];

    /// <summary>Creates the declaration, or fails with one error for every package that has more than one owner.</summary>
    /// <param name="declared">The requirements per manager. A manager with none is allowed and ignored.</param>
    /// <param name="field">The field name used in validation errors.</param>
    /// <returns>The declaration or a validation error that names each offending package and the managers involved.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="declared"/> or one of its lists is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A key is not a defined <see cref="PackageManagerKind"/>.</exception>
    public static Result<DeclaredPackages> Create(IReadOnlyDictionary<PackageManagerKind, IReadOnlyList<PackageRequirement>> declared, string field = "packages")
    {
        ArgumentNullException.ThrowIfNull(declared);

        var errors = new ValidationErrors();
        Dictionary<PackageName, List<PackageManagerKind>> owners = [];
        foreach (var (manager, requirements) in declared.OrderBy(static pair => pair.Key))
        {
            ArgumentNullException.ThrowIfNull(requirements);
            if (!Enum.IsDefined(manager))
            {
                throw new ArgumentOutOfRangeException(nameof(declared), "A package manager is not a defined value.");
            }

            foreach (var requirement in requirements)
            {
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

        var groups = declared
            .Where(static pair => pair.Value.Count > 0)
            .OrderBy(static pair => pair.Key)
            .Select(static pair => (pair.Key, pair.Value.OrderBy(static r => r.Name.Value, StringComparer.Ordinal).ToArray()))
            .ToArray();
        return Result.Ok(new DeclaredPackages(groups));
    }

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        foreach (var (manager, requirements) in _groups)
        {
            yield return manager;
            foreach (var requirement in requirements)
            {
                yield return requirement;
            }
        }
    }
}
