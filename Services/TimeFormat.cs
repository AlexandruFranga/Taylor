namespace WorkTimeBot.Services;

public static class TimeFormat
{
    public static string Humanize(TimeSpan span)
    {
        if (span < TimeSpan.Zero)
        {
            span = TimeSpan.Zero;
        }

        var totalMinutes = (int)span.TotalMinutes;
        var hours = totalMinutes / 60;
        var minutes = totalMinutes % 60;

        if (hours == 0 && minutes == 0)
        {
            return "less than a minute";
        }

        if (hours == 0)
        {
            return $"{minutes}m";
        }

        if (minutes == 0)
        {
            return $"{hours}h";
        }

        return $"{hours}h {minutes}m";
    }
}
