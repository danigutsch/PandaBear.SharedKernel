using PandaBear.BuildingBlocks;
using PandaBear.Results;

namespace PandaBear.Packages;

/// <summary>
/// The name of a package: 1 to 214 ASCII letters, digits, and <c>. _ + - : @ /</c>, starting with a letter, digit, or
/// <c>@</c>. The text is kept exactly, because package managers compare names case-sensitively.
/// </summary>
public sealed class PackageName : ValueObject
{
    /// <summary>The longest allowed name.</summary>
    public const int MaxLength = 214;

    private PackageName(string value) => Value = value;

    /// <summary>Gets the name.</summary>
    public string Value
    {
        get;
    }

    /// <summary>Creates a name from <paramref name="value"/>, which is not trimmed or changed.</summary>
    /// <param name="value">The candidate name.</param>
    /// <param name="field">The field name used in validation errors.</param>
    /// <returns>The name or a validation error that never echoes the input.</returns>
    public static Result<PackageName> Create(string? value, string field = "name")
    {
        string? problem = value switch
        {
            null or "" => "Package name is required.",
            { Length: > MaxLength } => $"Package name cannot exceed {MaxLength} characters.",
            _ when !char.IsAsciiLetterOrDigit(value[0]) && value[0] != '@' => "Package name must start with a letter, digit, or '@'.",
            _ when !value.All(static c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '+' or '-' or ':' or '@' or '/') =>
                "Package name can only contain letters, digits, and '.', '_', '+', '-', ':', '@', '/'.",
            _ => null,
        };
        return problem is null ? Result.Ok(new PackageName(value!)) : Result.Invalid<PackageName>(problem, field, problem);
    }

    /// <inheritdoc />
    public override string ToString() => Value;

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
