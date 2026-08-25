namespace WorkTimeBot.Models;

public class BotConfig
{
  
    public string Token { get; set; } = "";

    public ulong? GuildId { get; set; }
    
    public ulong? WeeklyChannelId { get; set; }

    public string TimeZoneId { get; set; } = "Europe/Bucharest";

    public int WeeklyPostHourLocal { get; set; } = 9;

    public string? ConnectionString { get; set; }
}
