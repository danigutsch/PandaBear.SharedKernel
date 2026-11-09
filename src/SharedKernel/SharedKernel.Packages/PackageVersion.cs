using PandaBear.BuildingBlocks;
using PandaBear.Results;

namespace PandaBear.Packages;

/// <summary>
/// The version text of a package: 1 to 128 printable ASCII characters without spaces. Version order is specific to
/// each package manager, so this type only compares for equality, exactly as written.
/// </summary>
public sealed class PackageVersion : ValueObject
{
    /// <summary>The longest allowed version text.</summary>
    public const int MaxLength = 128;

    private PackageVersion(string value) => Value = value;

    /// <summary>Gets the version text.</summary>
    public string Value
    {
        get;
    }

    /// <summary>Creates a version from <paramref name="value"/>, which is not trimmed or changed.</summary>
    /// <param name="value">The candidate version text.</param>
    /// <param name="field">The field name used in validation errors.</param>
    /// <returns>The version or a validation error that never echoes the input.</returns>
    public static Result<PackageVersion> Create(string? value, string field = "version")
    {
        string? problem = value switch
        {
            null or "" => "Package version is required.",
            { Length: > MaxLength } => $"Package version cannot exceed {MaxLength} characters.",
            _ when !value.All(static c => c is > ' ' and < '\u007f') => "Package version can only contain printable ASCII characters without spaces.",
            _ => null,
        };
        return problem is null ? Result.Ok(new PackageVersion(value!)) : Result.Invalid<PackageVersion>(problem, field, problem);
    }

    /// <inheritdoc />
    public override string ToString() => Value;

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
