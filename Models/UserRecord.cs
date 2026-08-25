namespace WorkTimeBot.Models;

public class UserRecord
{
    public ulong GuildId { get; set; }
    public ulong UserId { get; set; }
    public string DisplayName { get; set; } = "Unknown";

    public DateTimeOffset? CurrentStart { get; set; }

    public string? CurrentNote { get; set; }

    public List<Session> Sessions { get; set; } = new();
}
