namespace WorkTimeBot.Models;

public class Session
{
    public DateTimeOffset Start { get; set; }
    public DateTimeOffset End { get; set; }

    public string? Note { get; set; }

    public TimeSpan Duration => End - Start;
}
