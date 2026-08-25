using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
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

string connectionString;
try
{
    connectionString = PostgresConnectionString.Resolve(config);
}
catch (Exception ex)
{
    Console.WriteLine(ex.Message);
    return;
}

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton(config);
builder.Services.AddPooledDbContextFactory<WorkTimeDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddSingleton<TrackingStore>();

builder.Services.AddSingleton(new DiscordSocketClient(new DiscordSocketConfig
{
    GatewayIntents = GatewayIntents.Guilds,
}));

builder.Services.AddSingleton(sp => new Discord.Interactions.InteractionService(
    sp.GetRequiredService<DiscordSocketClient>()));

builder.Services.AddHostedService<BotHostedService>();
builder.Services.AddHostedService<WeeklyScheduler>();

var host = builder.Build();

using (var scope = host.Services.CreateScope())
{
    var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<WorkTimeDbContext>>();
    await using var db = await contextFactory.CreateDbContextAsync();
    await db.Database.MigrateAsync();
}

var store = host.Services.GetRequiredService<TrackingStore>();
if (config.GuildId is ulong seedGuildId
    && config.WeeklyChannelId is ulong seedChannelId
    && await store.GetWeeklyChannelIdAsync(seedGuildId) is null)
{
    await store.SetWeeklyChannelIdAsync(seedGuildId, seedChannelId);
}

await host.RunAsync();
