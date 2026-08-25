namespace WorkTimeBot.Models;

public class Session
{
    public int Id { get; set; }
    public ulong GuildId { get; set; }
    public ulong UserId { get; set; }
    public DateTimeOffset Start { get; set; }
    public DateTimeOffset End { get; set; }
    public string? Note { get; set; }

    public TimeSpan Duration => End - Start;
}
