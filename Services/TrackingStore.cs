using System.Text.Json;
using WorkTimeBot.Models;

namespace WorkTimeBot.Services;

public class TrackingStore
{
    private readonly string _filePath;
    private readonly object _lock = new();
    private TrackingData _data;

    public TrackingStore(string filePath)
    {
        _filePath = filePath;
        _data = Load();
    }

    private TrackingData Load()
    {
        if (!File.Exists(_filePath))
        {
            return new TrackingData();
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<TrackingData>(json) ?? new TrackingData();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TrackingStore] Failed to read {_filePath}: {ex.Message}. Starting with empty data.");
            return new TrackingData();
        }
    }

    private void Save()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(_data, options);

        var tmpPath = _filePath + ".tmp";
        File.WriteAllText(tmpPath, json);
        File.Move(tmpPath, _filePath, overwrite: true);
    }

    public ulong? GetWeeklyChannelId()
    {
        lock (_lock)
        {
            return _data.WeeklyChannelId;
        }
    }

    public void SetWeeklyChannelId(ulong channelId)
    {
        lock (_lock)
        {
            _data.WeeklyChannelId = channelId;
            Save();
        }
    }

    private UserRecord GetOrCreate(ulong userId, string displayName)
    {
        var key = userId.ToString();
        if (!_data.Users.TryGetValue(key, out var record))
        {
            record = new UserRecord { UserId = userId, DisplayName = displayName };
            _data.Users[key] = record;
        }
        else
        {
            record.DisplayName = displayName;
        }

        return record;
    }

    public (bool ok, string message) Start(ulong userId, string displayName, string? note)
    {
        lock (_lock)
        {
            var record = GetOrCreate(userId, displayName);
            if (record.CurrentStart.HasValue)
            {
                return (false, $"You already have a timer running (started <t:{record.CurrentStart.Value.ToUnixTimeSeconds()}:R>).");
            }

            record.CurrentStart = DateTimeOffset.UtcNow;
            record.CurrentNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
            Save();
            return (true, "Timer started.");
        }
    }

    public (bool ok, string message, TimeSpan duration, TimeSpan totalToday, string? note) Finish(ulong userId, string displayName)
    {
        lock (_lock)
        {
            var record = GetOrCreate(userId, displayName);
            if (!record.CurrentStart.HasValue)
            {
                return (false, "You don't have a timer running. Use /start first.", TimeSpan.Zero, TimeSpan.Zero, null);
            }

            var start = record.CurrentStart.Value;
            var end = DateTimeOffset.UtcNow;
            var note = record.CurrentNote;
            record.Sessions.Add(new Session { Start = start, End = end, Note = note });
            record.CurrentStart = null;
            record.CurrentNote = null;
            Save();

            var duration = end - start;
            var totalToday = SumSessionsOnUtcDate(record, DateTime.UtcNow.Date);

            return (true, "Timer stopped.", duration, totalToday, note);
        }
    }

    public (bool running, DateTimeOffset? start, TimeSpan elapsed, TimeSpan totalToday, string? note) GetStatus(ulong userId)
    {
        lock (_lock)
        {
            var key = userId.ToString();
            if (!_data.Users.TryGetValue(key, out var record))
            {
                return (false, null, TimeSpan.Zero, TimeSpan.Zero, null);
            }

            var now = DateTimeOffset.UtcNow;
            var elapsed = record.CurrentStart.HasValue ? now - record.CurrentStart.Value : TimeSpan.Zero;

            var totalToday = SumSessionsOnUtcDate(record, DateTime.UtcNow.Date);
            if (record.CurrentStart.HasValue && record.CurrentStart.Value.UtcDateTime.Date == DateTime.UtcNow.Date)
            {
                totalToday += elapsed;
            }

            return (record.CurrentStart.HasValue, record.CurrentStart, elapsed, totalToday, record.CurrentNote);
        }
    }

    public List<(ulong userId, string displayName, TimeSpan total)> GetTotalsForRange(DateTimeOffset rangeStartUtc, DateTimeOffset rangeEndUtc)
    {
        lock (_lock)
        {
            var results = new List<(ulong, string, TimeSpan)>();

            foreach (var record in _data.Users.Values)
            {
                var total = record.Sessions
                    .Where(s => s.Start >= rangeStartUtc && s.Start < rangeEndUtc)
                    .Aggregate(TimeSpan.Zero, (sum, s) => sum + s.Duration);

                if (record.CurrentStart.HasValue
                    && record.CurrentStart.Value >= rangeStartUtc
                    && record.CurrentStart.Value < rangeEndUtc)
                {
                    total += DateTimeOffset.UtcNow - record.CurrentStart.Value;
                }

                if (total > TimeSpan.Zero)
                {
                    results.Add((record.UserId, record.DisplayName, total));
                }
            }

            return results.OrderByDescending(r => r.Item3).ToList();
        }
    }

    private static TimeSpan SumSessionsOnUtcDate(UserRecord record, DateTime utcDate)
    {
        return record.Sessions
            .Where(s => s.Start.UtcDateTime.Date == utcDate)
            .Aggregate(TimeSpan.Zero, (sum, s) => sum + s.Duration);
    }


    public UserRecord? GetRecord(ulong userId)
    {
        lock (_lock)
        {
            _data.Users.TryGetValue(userId.ToString(), out var record);
            
            return record;
        }
    }
}
