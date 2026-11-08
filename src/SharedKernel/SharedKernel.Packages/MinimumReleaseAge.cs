using PandaBear.BuildingBlocks;
using PandaBear.Results;

namespace PandaBear.Packages;

/// <summary>How long a release must have been public before it may be installed: 0 to 3650 whole days.</summary>
public sealed class MinimumReleaseAge : ValueObject
{
    /// <summary>The longest allowed age, in days.</summary>
    public const int MaxDays = 3650;

    private MinimumReleaseAge(TimeSpan value) => Value = value;

    /// <summary>Gets the age as a time span of whole days.</summary>
    public TimeSpan Value
    {
        get;
    }

    /// <summary>Creates an age of <paramref name="days"/> whole days.</summary>
    /// <param name="days">The number of days, from 0 to <see cref="MaxDays"/>.</param>
    /// <param name="field">The field name used in validation errors.</param>
    /// <returns>The age or a validation error.</returns>
    public static Result<MinimumReleaseAge> FromDays(int days, string field = "minimumReleaseAge")
    {
        const string message = "Minimum release age must be from 0 to 3650 days.";
        return days is >= 0 and <= MaxDays
            ? Result.Ok(new MinimumReleaseAge(TimeSpan.FromDays(days)))
            : Result.Invalid<MinimumReleaseAge>(message, field, message);
    }

    /// <inheritdoc />
    public override string ToString() => $"{Value.Days} days";

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
