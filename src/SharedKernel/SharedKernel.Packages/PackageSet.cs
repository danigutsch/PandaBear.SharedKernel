using SharedKernel.Domain;
using SharedKernel.Results;

namespace SharedKernel.Packages;

/// <summary>An immutable, name-ordered set of requirements with one requirement per package name.</summary>
public sealed class PackageSet : ValueObject
{
    private PackageSet(PackageRequirement[] requirements) => Requirements = Array.AsReadOnly(requirements);

    /// <summary>Gets an empty set.</summary>
    public static PackageSet Empty { get; } = new([]);

    /// <summary>Gets the requirements ordered by their ordinal package names.</summary>
    public IReadOnlyList<PackageRequirement> Requirements { get; }

    /// <summary>Creates a set from requirements whose names are unique.</summary>
    /// <param name="requirements">The requirements to snapshot.</param>
    /// <param name="field">The field name used in validation errors.</param>
    /// <returns>The package set or a validation error if a name occurs more than once.</returns>
    /// <exception cref="ArgumentNullException">The input or one of its requirements is <see langword="null"/>.</exception>
    public static Result<PackageSet> Create(IEnumerable<PackageRequirement> requirements, string field = "packages")
    {
        ArgumentNullException.ThrowIfNull(requirements);

        HashSet<PackageName> names = [];
        List<PackageRequirement> snapshot = [];
        foreach (var requirement in requirements)
        {
            ArgumentNullException.ThrowIfNull(requirement, nameof(requirements));
            if (!names.Add(requirement.Name))
            {
                var message = $"The package '{requirement.Name}' is required more than once.";
                return Result.Invalid<PackageSet>(message, field, message);
            }

            snapshot.Add(requirement);
        }

        return Result.Ok(snapshot.Count == 0
            ? Empty
            : new PackageSet([.. snapshot.OrderBy(static requirement => requirement.Name.Value, StringComparer.Ordinal)]));
    }

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents() => Requirements;
}
