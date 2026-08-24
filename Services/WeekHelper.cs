namespace WorkTimeBot.Services;

public static class WeekHelper
{
    public static (DateTimeOffset startUtc, DateTimeOffset endUtc) GetWeekRangeUtc(
        DateTimeOffset referenceUtc, TimeZoneInfo tz, int weeksAgo = 0)
    {
        var referenceLocal = TimeZoneInfo.ConvertTime(referenceUtc, tz);
        var referenceLocalDate = referenceLocal.Date;

        var diffFromMonday = ((int)referenceLocalDate.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        var mondayLocalMidnight = referenceLocalDate.AddDays(-diffFromMonday - (7 * weeksAgo));

        var weekStartUtc = TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(mondayLocalMidnight, DateTimeKind.Unspecified), tz);
        var weekEndUtc = TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(mondayLocalMidnight.AddDays(7), DateTimeKind.Unspecified), tz);

        return (new DateTimeOffset(weekStartUtc, TimeSpan.Zero), new DateTimeOffset(weekEndUtc, TimeSpan.Zero));
    }
}
