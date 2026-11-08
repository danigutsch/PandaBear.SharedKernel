using PandaBear.Results;

namespace PandaBear.BuildingBlocks;

/// <summary>A short code made of ASCII letters, digits, <c>-</c> and <c>_</c>, stored in upper case so codes that differ only in case are the same code.</summary>
public sealed class ReferenceCode : ValueObject
{
    private ReferenceCode(string value) => Value = value;

    /// <summary>Gets the upper-case code.</summary>
    public string Value
    {
        get;
    }

    /// <summary>Creates a code from text; surrounding whitespace is removed and letters are upper-cased.</summary>
    /// <param name="value">The candidate code.</param>
    /// <param name="maxLength">The longest allowed code.</param>
    /// <param name="field">The field name used in validation errors.</param>
    /// <param name="label">The label used in validation messages.</param>
    /// <returns>The code or a validation error that never echoes the input.</returns>
    public static Result<ReferenceCode> Create(string? value, int maxLength, string field = "code", string label = "Code")
    {
        var errors = new ValidationErrors();
        var sanitized = TextRules.Required(errors, value, field, label, maxLength);
        if (!errors.HasErrors && !sanitized.All(static character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_'))
        {
            var message = $"{label} can only contain letters, digits, '-' and '_'.";
            errors.Add(Result.Invalid(detail: message, field: field, message: message));
        }

        return errors.HasErrors ? errors.ToResult<ReferenceCode>() : Result.Ok(new ReferenceCode(sanitized.ToUpperInvariant()));
    }

    /// <inheritdoc />
    public override string ToString() => Value;

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
