namespace WorkTimeBot.Models;

public class BotSetting
{
    public ulong GuildId { get; set; }
    public string Key { get; set; } = "";
    public string? Value { get; set; }
}
