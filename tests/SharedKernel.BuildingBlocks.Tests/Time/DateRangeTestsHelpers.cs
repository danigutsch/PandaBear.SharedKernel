namespace PandaBear.BuildingBlocks.Tests;

/// <summary>
/// Helpers for building dates in the DateRange tests.
/// </summary>
internal static class DateRangeTestsHelpers
{
    /// <summary>
    /// Creates a DateTime with DateTimeKind.Utc from the given date and optional time parts.
    /// </summary>
    public static DateTime UtcDate(int year, int month, int day, int hour = 0, int minute = 0, int second = 0)
    {
        return new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc);
    }
}
