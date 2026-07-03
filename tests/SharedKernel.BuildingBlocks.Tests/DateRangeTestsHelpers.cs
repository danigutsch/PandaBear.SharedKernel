namespace SharedKernel.BuildingBlocks.Tests;

internal static class DateRangeTestsHelpers
{
    public static DateTime UtcDate(int year, int month, int day, int hour = 0, int minute = 0, int second = 0)
    {
        return new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc);
    }
}
