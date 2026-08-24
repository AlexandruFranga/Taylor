namespace WorkTimeBot.Services;
using WorkTimeBot.Models;

public static class TotalTime
{
    public static TimeSpan Calculate(UserRecord record)
    {
        var total = record.Sessions.Aggregate(TimeSpan.Zero, (sum, s) => sum + s.Duration);

        if (record.CurrentStart.HasValue)
        {
            total += DateTimeOffset.UtcNow - record.CurrentStart.Value;
        }

        return total;

    }
}