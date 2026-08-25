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

    public async Task<ulong?> GetWeeklyChannelIdAsync()
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var setting = await db.Settings.FindAsync(WeeklyChannelKey);

        return setting?.Value is string value && ulong.TryParse(value, out var channelId) ? channelId : null;
    }

    public async Task SetWeeklyChannelIdAsync(ulong channelId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var setting = await db.Settings.FindAsync(WeeklyChannelKey);

        if (setting is null)
        {
            db.Settings.Add(new BotSetting { Key = WeeklyChannelKey, Value = channelId.ToString() });
        }
        else
        {
            setting.Value = channelId.ToString();
        }

        await db.SaveChangesAsync();
    }

    private static async Task<UserRecord> GetOrCreateAsync(WorkTimeDbContext db, ulong userId, string displayName)
    {
        var record = await db.Users.FindAsync(userId);
        if (record is null)
        {
            record = new UserRecord { UserId = userId, DisplayName = displayName };
            db.Users.Add(record);
        }
        else
        {
            record.DisplayName = displayName;
        }

        return record;
    }

    public async Task<(bool ok, string message)> StartAsync(ulong userId, string displayName, string? note)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var record = await GetOrCreateAsync(db, userId, displayName);

        if (record.CurrentStart.HasValue)
        {
            return (false, $"You already have a timer running (started <t:{record.CurrentStart.Value.ToUnixTimeSeconds()}:R>).");
        }

        record.CurrentStart = DateTimeOffset.UtcNow;
        record.CurrentNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        await db.SaveChangesAsync();

        return (true, "Timer started.");
    }

    public async Task<(bool ok, string message, TimeSpan duration, TimeSpan totalToday, string? note)> FinishAsync(ulong userId, string displayName)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var record = await GetOrCreateAsync(db, userId, displayName);

        if (!record.CurrentStart.HasValue)
        {
            return (false, "You don't have a timer running. Use /start first.", TimeSpan.Zero, TimeSpan.Zero, null);
        }

        var start = record.CurrentStart.Value;
        var end = DateTimeOffset.UtcNow;
        var note = record.CurrentNote;

        db.Sessions.Add(new Session { UserId = userId, Start = start, End = end, Note = note });
        record.CurrentStart = null;
        record.CurrentNote = null;
        await db.SaveChangesAsync();

        var duration = end - start;
        var totalToday = await SumSessionsOnUtcDateAsync(db, userId, DateTime.UtcNow.Date);

        return (true, "Timer stopped.", duration, totalToday, note);
    }

    public async Task<(bool running, DateTimeOffset? start, TimeSpan elapsed, TimeSpan totalToday, string? note)> GetStatusAsync(ulong userId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var record = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.UserId == userId);

        if (record is null)
        {
            return (false, null, TimeSpan.Zero, TimeSpan.Zero, null);
        }

        var now = DateTimeOffset.UtcNow;
        var elapsed = record.CurrentStart.HasValue ? now - record.CurrentStart.Value : TimeSpan.Zero;

        var totalToday = await SumSessionsOnUtcDateAsync(db, userId, DateTime.UtcNow.Date);
        if (record.CurrentStart.HasValue && record.CurrentStart.Value.UtcDateTime.Date == DateTime.UtcNow.Date)
        {
            totalToday += elapsed;
        }

        return (record.CurrentStart.HasValue, record.CurrentStart, elapsed, totalToday, record.CurrentNote);
    }

    public async Task<List<(ulong userId, string displayName, TimeSpan total)>> GetTotalsForRangeAsync(DateTimeOffset rangeStartUtc, DateTimeOffset rangeEndUtc)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        var sessionTotals = await db.Sessions
            .Where(s => s.Start >= rangeStartUtc && s.Start < rangeEndUtc)
            .Select(s => new { s.UserId, s.Start, s.End })
            .ToListAsync();

        var runningRecords = await db.Users
            .Where(u => u.CurrentStart != null && u.CurrentStart >= rangeStartUtc && u.CurrentStart < rangeEndUtc)
            .Select(u => new { u.UserId, u.CurrentStart })
            .ToListAsync();

        var displayNames = await db.Users
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

    private static async Task<TimeSpan> SumSessionsOnUtcDateAsync(WorkTimeDbContext db, ulong userId, DateTime utcDate)
    {
        var dayStart = new DateTimeOffset(utcDate, TimeSpan.Zero);
        var dayEnd = dayStart.AddDays(1);

        var sessions = await db.Sessions
            .Where(s => s.UserId == userId && s.Start >= dayStart && s.Start < dayEnd)
            .Select(s => new { s.Start, s.End })
            .ToListAsync();

        return sessions.Aggregate(TimeSpan.Zero, (sum, s) => sum + (s.End - s.Start));
    }

    public async Task<UserRecord?> GetRecordAsync(ulong userId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        return await db.Users.AsNoTracking().Include(u => u.Sessions).SingleOrDefaultAsync(u => u.UserId == userId);
    }
}
