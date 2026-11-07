using PandaBear.Results;

namespace PandaBear.BuildingBlocks;

/// <summary>A person's first and last name, sanitized; not a legal-identity check.</summary>
public sealed class PersonName : ValueObject
{
    /// <summary>The maximum length of each part.</summary>
    public const int MaxPartLength = 128;

    private PersonName(string firstName, string lastName)
    {
        FirstName = firstName;
        LastName = lastName;
    }

    /// <summary>Gets the first name. Personal data.</summary>
    public string FirstName
    {
        get;
    }

    /// <summary>Gets the last name. Personal data.</summary>
    public string LastName
    {
        get;
    }

    /// <summary>Gets the first and last name joined by a space.</summary>
    public string FullName => $"{FirstName} {LastName}";

    /// <summary>Creates a name from both parts; each is required and sanitized.</summary>
    /// <param name="firstName">The first name.</param>
    /// <param name="lastName">The last name.</param>
    /// <returns>The name or validation errors for each invalid part.</returns>
    public static Result<PersonName> Create(string? firstName, string? lastName)
    {
        var errors = new ValidationErrors();
        var first = TextRules.Required(errors, firstName, "firstName", "First name", MaxPartLength);
        var last = TextRules.Required(errors, lastName, "lastName", "Last name", MaxPartLength);

        return errors.HasErrors ? errors.ToResult<PersonName>() : Result.Ok(new PersonName(first, last));
    }

    /// <inheritdoc />
    public override string ToString() => "PersonName [redacted]";

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return FirstName;
        yield return LastName;
    }
}
