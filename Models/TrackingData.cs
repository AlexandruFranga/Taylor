namespace WorkTimeBot.Models;

public class TrackingData
{
    public Dictionary<string, UserRecord> Users { get; set; } = new();

    public ulong? WeeklyChannelId { get; set; }
}
