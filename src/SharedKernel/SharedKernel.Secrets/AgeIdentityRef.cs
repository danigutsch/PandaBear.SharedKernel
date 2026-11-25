using SharedKernel.BuildingBlocks;
using SharedKernel.Results;

namespace SharedKernel.Secrets;

/// <summary>
/// The absolute path of an age identity file, never the key material. Text that looks like an age secret key
/// (<c>AGE-SECRET-KEY-1...</c>) or a plugin identity (<c>AGE-PLUGIN-...</c>) is rejected with an error that says to pass a
/// file. A plugin identity is referenced the same way: the file holds the plugin's identity stub.
/// </summary>
public sealed class AgeIdentityRef : ValueObject
{
    /// <summary>The longest accepted path.</summary>
    public const int MaxLength = 4096;

    private AgeIdentityRef(string path) => Path = path;

    /// <summary>Gets the absolute path of the identity file.</summary>
    public string Path
    {
        get;
    }

    /// <summary>Creates a reference from <paramref name="value"/>.</summary>
    /// <param name="value">The candidate absolute file path.</param>
    /// <param name="field">The field name used in validation errors.</param>
    /// <returns>The reference or a validation error that never echoes the input.</returns>
    public static Result<AgeIdentityRef> Create(string? value, string field = "identity")
    {
        string? problem = value switch
        {
            null or "" => "Identity file path is required.",
            _ when value.Contains("AGE-SECRET-KEY-", StringComparison.OrdinalIgnoreCase) || value.Contains("AGE-PLUGIN-", StringComparison.OrdinalIgnoreCase) =>
                "This looks like age key material. Pass the path of an identity file, never the key itself.",
            { Length: > MaxLength } => $"Identity file path cannot exceed {MaxLength} characters.",
            _ when value.Any(char.IsControl) => "Identity file path cannot contain control characters.",
            _ when !System.IO.Path.IsPathFullyQualified(value) => "Identity file path must be absolute.",
            _ when value.EndsWith('/', StringComparison.Ordinal) || value.Split('/', '\\').Contains("..") => "Identity file path must name a file without '..' segments.",
            _ => null,
        };
        return problem is null ? Result.Ok(new AgeIdentityRef(value!)) : Result.Invalid<AgeIdentityRef>(problem, field, problem);
    }

    /// <inheritdoc />
    public override string ToString() => Path;

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Path;
    }
}
