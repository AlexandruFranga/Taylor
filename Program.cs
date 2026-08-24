using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WorkTimeBot.Models;
using WorkTimeBot.Services;

var baseDir = Directory.GetCurrentDirectory();
var configPath = Path.Combine(baseDir, "config.json");

BotConfig config;
if (File.Exists(configPath))
{
    try
    {
        config = System.Text.Json.JsonSerializer.Deserialize<BotConfig>(File.ReadAllText(configPath))
                 ?? throw new InvalidOperationException("config.json parsed to null.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Failed to parse config.json: {ex.Message}");
        return;
    }
}
else
{
    config = new BotConfig();
}

var envToken = Environment.GetEnvironmentVariable("DISCORD_BOT_TOKEN");
if (!string.IsNullOrWhiteSpace(envToken))
{
    config.Token = envToken;
}

if (ulong.TryParse(Environment.GetEnvironmentVariable("DISCORD_GUILD_ID"), out var envGuildId))
{
    config.GuildId = envGuildId;
}

if (ulong.TryParse(Environment.GetEnvironmentVariable("DISCORD_WEEKLY_CHANNEL_ID"), out var envWeeklyChannelId))
{
    config.WeeklyChannelId = envWeeklyChannelId;
}

if (string.IsNullOrWhiteSpace(config.Token))
{
    Console.WriteLine("No bot token found. Set it via the DISCORD_BOT_TOKEN environment variable, or add a Token to config.json. Get one from the Discord Developer Portal.");
    return;
}

try
{
    TimeZoneInfo.FindSystemTimeZoneById(config.TimeZoneId);
}
catch (Exception ex)
{
    Console.WriteLine($"config.json TimeZoneId \"{config.TimeZoneId}\" is not valid: {ex.Message}");
    return;
}

var dataDir = Path.Combine(baseDir, "data");
Directory.CreateDirectory(dataDir);
var dataPath = Path.Combine(dataDir, "data.json");

var store = new TrackingStore(dataPath);
if (store.GetWeeklyChannelId() is null && config.WeeklyChannelId is ulong seedChannelId)
{
    store.SetWeeklyChannelId(seedChannelId);
}

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton(config);
builder.Services.AddSingleton(store);

builder.Services.AddSingleton(new DiscordSocketClient(new DiscordSocketConfig
{
    GatewayIntents = GatewayIntents.Guilds,
}));

builder.Services.AddSingleton(sp => new Discord.Interactions.InteractionService(
    sp.GetRequiredService<DiscordSocketClient>()));

builder.Services.AddHostedService<BotHostedService>();
builder.Services.AddHostedService<WeeklyScheduler>();

var host = builder.Build();
await host.RunAsync();
