using Discord.Interactions;
using WorkTimeBot.Models;
using WorkTimeBot.Services;

namespace WorkTimeBot.Modules;

public class TrackingModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly TrackingStore _store;
    private readonly TimeZoneInfo _tz;

    public TrackingModule(TrackingStore store, BotConfig config)
    {
        _store = store;
        _tz = TimeZoneInfo.FindSystemTimeZoneById(config.TimeZoneId);
    }

    [SlashCommand("start", "Start tracking your work time.")]
    public async Task StartAsync(
        [Summary("note", "What are you about to work on?")] string? note = null)
    {
        var (ok, message) = _store.Start(Context.User.Id, Context.User.Username, note);

        if (!ok)
        {
            await RespondAsync($"⏱️ {message}", ephemeral: true);
            return;
        }

        var trimmedNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        var suffix = trimmedNote is null ? "." : $" on: **{trimmedNote}**";
        await RespondAsync($"▶️ **{Context.User.Username}** started working{suffix}");
    }

    [SlashCommand("finish", "Stop tracking your work time.")]
    public async Task FinishAsync()
    {
        var (ok, message, duration, totalToday, note) = _store.Finish(Context.User.Id, Context.User.Username);

        if (!ok)
        {
            await RespondAsync($"⏱️ {message}", ephemeral: true);
            return;
        }

        var noteSuffix = string.IsNullOrWhiteSpace(note) ? "" : $" (*{note}*)";
        await RespondAsync(
            $"⏹️ **{Context.User.Username}** stopped working{noteSuffix} — session: **{TimeFormat.Humanize(duration)}** " +
            $"(today's total: **{TimeFormat.Humanize(totalToday)}**).");
    }

    [SlashCommand("status", "Check your current timer.")]
    public async Task StatusAsync()
    {
        var (running, start, elapsed, totalToday, note) = _store.GetStatus(Context.User.Id);

        if (!running)
        {
            await RespondAsync(
                $"⏱️ No timer running. Today's total so far: **{TimeFormat.Humanize(totalToday)}**.",
                ephemeral: true);
            return;
        }

        var noteSuffix = string.IsNullOrWhiteSpace(note) ? "" : $" — working on: **{note}**";
        await RespondAsync(
            $"🟢 Timer running since <t:{start!.Value.ToUnixTimeSeconds()}:t> " +
            $"(elapsed: **{TimeFormat.Humanize(elapsed)}**, today's total: **{TimeFormat.Humanize(totalToday)}**){noteSuffix}.",
            ephemeral: true);
    }

    [SlashCommand("weekly", "See this week's work time leaderboard.")]
    public async Task WeeklyAsync()
    {
        var (start, end) = WeekHelper.GetWeekRangeUtc(DateTimeOffset.UtcNow, _tz, weeksAgo: 0);
        var totals = _store.GetTotalsForRange(start, end);
        var embed = ReportBuilder.BuildWeeklyEmbed(totals, start, end, _tz, live: true);

        await RespondAsync(embed: embed);
    }
}
