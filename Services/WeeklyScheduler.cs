using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using WorkTimeBot.Models;

namespace WorkTimeBot.Services;

public class WeeklyScheduler : BackgroundService
{
    private readonly DiscordSocketClient _client;
    private readonly TrackingStore _store;
    private readonly BotConfig _config;
    private readonly TimeZoneInfo _tz;

    public WeeklyScheduler(DiscordSocketClient client, TrackingStore store, BotConfig config)
    {
        _client = client;
        _store = store;
        _config = config;
        _tz = TimeZoneInfo.FindSystemTimeZoneById(config.TimeZoneId);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (_client.ConnectionState != ConnectionState.Connected && !stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            if (await _store.GetWeeklyChannelIdAsync() is null)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }

                continue;
            }

            var delay = GetDelayUntilNextRun();
            Console.WriteLine($"[WeeklyScheduler] Next automatic weekly report in {delay}.");

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }

            try
            {
                await PostWeeklyReportAsync(weeksAgo: 1);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WeeklyScheduler] Failed to post weekly report: {ex.Message}");
            }
        }
    }

    private TimeSpan GetDelayUntilNextRun()
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var nowLocal = TimeZoneInfo.ConvertTime(nowUtc, _tz);

        var diffFromMonday = ((int)nowLocal.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        var thisMondayTarget = nowLocal.Date.AddDays(-diffFromMonday).AddHours(_config.WeeklyPostHourLocal);

        var targetLocal = nowLocal.DateTime >= thisMondayTarget
            ? thisMondayTarget.AddDays(7)
            : thisMondayTarget;

        var targetUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(targetLocal, DateTimeKind.Unspecified), _tz);
        var delay = targetUtc - nowUtc.UtcDateTime;

        return delay > TimeSpan.Zero ? delay : TimeSpan.FromSeconds(5);
    }

    public async Task PostWeeklyReportAsync(int weeksAgo)
    {
        if (await _store.GetWeeklyChannelIdAsync() is not ulong channelId)
        {
            return;
        }

        if (_client.GetChannel(channelId) is not IMessageChannel channel)
        {
            Console.WriteLine($"[WeeklyScheduler] Configured weekly channel {channelId} was not found, or isn't a text channel the bot can see.");
            return;
        }

        var (start, end) = WeekHelper.GetWeekRangeUtc(DateTimeOffset.UtcNow, _tz, weeksAgo);
        var totals = await _store.GetTotalsForRangeAsync(start, end);
        var embed = ReportBuilder.BuildWeeklyEmbed(totals, start, end, _tz, live: weeksAgo == 0, topN: 3);

        await channel.SendMessageAsync(embed: embed);
    }
}
