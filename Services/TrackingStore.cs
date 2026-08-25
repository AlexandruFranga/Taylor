using Microsoft.EntityFrameworkCore;
using WorkTimeBot.Models;

namespace WorkTimeBot.Services;

public class TrackingStore
{
    private const string WeeklyChannelKey = "WeeklyChannelId";

    private readonly IDbContextFactory<WorkTimeDbContext> _contextFactory;

    public TrackingStore(IDbContextFactory<WorkTimeDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<ulong?> GetWeeklyChannelIdAsync(ulong guildId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var setting = await db.Settings.FindAsync(guildId, WeeklyChannelKey);

        return setting?.Value is string value && ulong.TryParse(value, out var channelId) ? channelId : null;
    }

    public async Task<List<(ulong guildId, ulong channelId)>> GetWeeklyChannelsAsync()
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var settings = await db.Settings
            .AsNoTracking()
            .Where(s => s.Key == WeeklyChannelKey)
            .Select(s => new { s.GuildId, s.Value })
            .ToListAsync();

        var channels = new List<(ulong, ulong)>();
        foreach (var setting in settings)
        {
            if (ulong.TryParse(setting.Value, out var channelId))
            {
                channels.Add((setting.GuildId, channelId));
            }
        }

        return channels;
    }

    public async Task SetWeeklyChannelIdAsync(ulong guildId, ulong channelId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var setting = await db.Settings.FindAsync(guildId, WeeklyChannelKey);

        if (setting is null)
        {
            db.Settings.Add(new BotSetting { GuildId = guildId, Key = WeeklyChannelKey, Value = channelId.ToString() });
        }
        else
        {
            setting.Value = channelId.ToString();
        }

        await db.SaveChangesAsync();
    }

    private static async Task<UserRecord> GetOrCreateAsync(WorkTimeDbContext db, ulong guildId, ulong userId, string displayName)
    {
        var record = await db.Users.FindAsync(guildId, userId);
        if (record is null)
        {
            record = new UserRecord { GuildId = guildId, UserId = userId, DisplayName = displayName };
            db.Users.Add(record);
        }
        else
        {
            record.DisplayName = displayName;
        }

        return record;
    }

    public async Task<(bool ok, string message)> StartAsync(ulong guildId, ulong userId, string displayName, string? note)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var record = await GetOrCreateAsync(db, guildId, userId, displayName);

        if (record.CurrentStart.HasValue)
        {
            return (false, $"You already have a timer running (started <t:{record.CurrentStart.Value.ToUnixTimeSeconds()}:R>).");
        }

        record.CurrentStart = DateTimeOffset.UtcNow;
        record.CurrentNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        await db.SaveChangesAsync();

        return (true, "Timer started.");
    }

    public async Task<(bool ok, string message, TimeSpan duration, TimeSpan totalToday, string? note)> FinishAsync(ulong guildId, ulong userId, string displayName)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var record = await GetOrCreateAsync(db, guildId, userId, displayName);

        if (!record.CurrentStart.HasValue)
        {
            return (false, "You don't have a timer running. Use /start first.", TimeSpan.Zero, TimeSpan.Zero, null);
        }

        var start = record.CurrentStart.Value;
        var end = DateTimeOffset.UtcNow;
        var note = record.CurrentNote;

        db.Sessions.Add(new Session { GuildId = guildId, UserId = userId, Start = start, End = end, Note = note });
        record.CurrentStart = null;
        record.CurrentNote = null;
        await db.SaveChangesAsync();

        var duration = end - start;
        var totalToday = await SumSessionsOnUtcDateAsync(db, guildId, userId, DateTime.UtcNow.Date);

        return (true, "Timer stopped.", duration, totalToday, note);
    }

    public async Task<(bool running, DateTimeOffset? start, TimeSpan elapsed, TimeSpan totalToday, string? note)> GetStatusAsync(ulong guildId, ulong userId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var record = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.GuildId == guildId && u.UserId == userId);

        if (record is null)
        {
            return (false, null, TimeSpan.Zero, TimeSpan.Zero, null);
        }

        var now = DateTimeOffset.UtcNow;
        var elapsed = record.CurrentStart.HasValue ? now - record.CurrentStart.Value : TimeSpan.Zero;

        var totalToday = await SumSessionsOnUtcDateAsync(db, guildId, userId, DateTime.UtcNow.Date);
        if (record.CurrentStart.HasValue && record.CurrentStart.Value.UtcDateTime.Date == DateTime.UtcNow.Date)
        {
            totalToday += elapsed;
        }

        return (record.CurrentStart.HasValue, record.CurrentStart, elapsed, totalToday, record.CurrentNote);
    }

    public async Task<List<(ulong userId, string displayName, TimeSpan total)>> GetTotalsForRangeAsync(ulong guildId, DateTimeOffset rangeStartUtc, DateTimeOffset rangeEndUtc)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        var sessionTotals = await db.Sessions
            .Where(s => s.GuildId == guildId && s.Start >= rangeStartUtc && s.Start < rangeEndUtc)
            .Select(s => new { s.UserId, s.Start, s.End })
            .ToListAsync();

        var runningRecords = await db.Users
            .Where(u => u.GuildId == guildId && u.CurrentStart != null && u.CurrentStart >= rangeStartUtc && u.CurrentStart < rangeEndUtc)
            .Select(u => new { u.UserId, u.CurrentStart })
            .ToListAsync();

        var displayNames = await db.Users
            .Where(u => u.GuildId == guildId)
            .Select(u => new { u.UserId, u.DisplayName })
            .ToDictionaryAsync(u => u.UserId, u => u.DisplayName);

        var totals = new Dictionary<ulong, TimeSpan>();

        foreach (var s in sessionTotals)
        {
            totals[s.UserId] = totals.GetValueOrDefault(s.UserId) + (s.End - s.Start);
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var r in runningRecords)
        {
            totals[r.UserId] = totals.GetValueOrDefault(r.UserId) + (now - r.CurrentStart!.Value);
        }

        return totals
            .Where(kv => kv.Value > TimeSpan.Zero)
            .Select(kv => (kv.Key, displayNames.GetValueOrDefault(kv.Key, "Unknown"), kv.Value))
            .OrderByDescending(t => t.Value)
            .ToList();
    }

    private static async Task<TimeSpan> SumSessionsOnUtcDateAsync(WorkTimeDbContext db, ulong guildId, ulong userId, DateTime utcDate)
    {
        var dayStart = new DateTimeOffset(utcDate, TimeSpan.Zero);
        var dayEnd = dayStart.AddDays(1);

        var sessions = await db.Sessions
            .Where(s => s.GuildId == guildId && s.UserId == userId && s.Start >= dayStart && s.Start < dayEnd)
            .Select(s => new { s.Start, s.End })
            .ToListAsync();

        return sessions.Aggregate(TimeSpan.Zero, (sum, s) => sum + (s.End - s.Start));
    }

    public async Task<UserRecord?> GetRecordAsync(ulong guildId, ulong userId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        return await db.Users.AsNoTracking().Include(u => u.Sessions).SingleOrDefaultAsync(u => u.GuildId == guildId && u.UserId == userId);
    }
}
