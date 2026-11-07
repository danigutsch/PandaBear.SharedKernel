using PandaBear.Results;

namespace PandaBear.BuildingBlocks;

/// <summary>A date of birth that is present, not in the future, and within an allowed age range on a given day.</summary>
public sealed class BirthDate : ValueObject
{
    private BirthDate(DateOnly value) => Value = value;

    /// <summary>Gets the date of birth.</summary>
    public DateOnly Value
    {
        get;
    }

    /// <summary>
    /// Creates a date of birth after validating it against the age limits. Ages follow the 1 March convention for leap-day
    /// births: in a non-leap year the birthday is reached on 1 March, which is the stricter reading for minimum-age checks.
    /// </summary>
    /// <param name="value">The date of birth; the default value is reported as missing.</param>
    /// <param name="today">The current business date.</param>
    /// <param name="minAgeYears">The youngest allowed age in whole years.</param>
    /// <param name="maxAgeYears">The oldest allowed age in whole years.</param>
    /// <returns>The date of birth or a validation error on <c>birthDate</c>.</returns>
    public static Result<BirthDate> Create(DateOnly value, DateOnly today, int minAgeYears, int maxAgeYears)
    {
        if (value == default)
        {
            return Invalid("Birth date is required.");
        }

        if (value > today)
        {
            return Invalid("Birth date cannot be in the future.");
        }

        var age = AgeOn(value, today);
        if (age < minAgeYears)
        {
            return Invalid($"Age must be at least {minAgeYears} years.");
        }

        return age > maxAgeYears
            ? Invalid($"Age cannot exceed {maxAgeYears} years.")
            : Result.Ok(new BirthDate(value));
    }

    /// <summary>
    /// Gets the age in whole years on a day. A leap-day (29 February) birthday is reached on 29 February in leap years and on
    /// 1 March in other years, so a person born on 29 February completes a year one day later than <c>DateOnly.AddYears</c> implies.
    /// </summary>
    /// <param name="day">The day.</param>
    /// <returns>The completed years.</returns>
    public int AgeOn(DateOnly day) => AgeOn(Value, day);

    private static int AgeOn(DateOnly birthDate, DateOnly day)
    {
        var age = day.Year - birthDate.Year;
        var birthday = birthDate is { Month: 2, Day: 29 } && !DateTime.IsLeapYear(day.Year)
            ? new DateOnly(day.Year, 3, 1)
            : birthDate.AddYears(age);
        return birthday > day ? age - 1 : age;
    }

    private static Result<BirthDate> Invalid(string message) =>
        Result.Invalid<BirthDate>(detail: message, field: "birthDate", message: message);

    /// <inheritdoc />
    public override string ToString() => "BirthDate [redacted]";

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
