using Discord;

namespace WorkTimeBot.Services;

public static class ReportBuilder
{
    public static Embed BuildWeeklyEmbed(
        List<(ulong userId, string displayName, TimeSpan total)> totals,
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        TimeZoneInfo tz,
        bool live,
        int? topN = null)
    {
        if (topN is int n && totals.Count > n)
        {
            totals = totals.Take(n).ToList();
        }

        var startLocal = TimeZoneInfo.ConvertTime(startUtc, tz);
        var endLocal = TimeZoneInfo.ConvertTime(endUtc.AddSeconds(-1), tz);

        var builder = new EmbedBuilder()
            .WithTitle(live ? "📊 Weekly work time (so far)" : "📊 Weekly work time")
            .WithDescription($"{startLocal:MMM d} – {endLocal:MMM d, yyyy}")
            .WithColor(Color.Blue)
            .WithCurrentTimestamp();

        if (totals.Count == 0)
        {
            builder.AddField("No sessions", "Nobody logged any work time this week. KYS!");
            return builder.Build();
        }

        var medals = new[] { "🥇", "🥈", "🥉" };
        var lines = new List<string>();
        for (var i = 0; i < totals.Count; i++)
        {
            var prefix = i < medals.Length ? medals[i] : $"{i + 1}.";
            lines.Add($"{prefix} <@{totals[i].userId}> — **{TimeFormat.Humanize(totals[i].total)}**");
        }

        builder.AddField("Leaderboard", string.Join("\n", lines));

        if (totals.Count > 1)
        {
            builder.WithFooter($"{totals[0].displayName} is in the lead this week");
        }

        return builder.Build();
    }
}
