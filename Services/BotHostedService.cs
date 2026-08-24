using System.Reflection;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using WorkTimeBot.Models;

namespace WorkTimeBot.Services;

public class BotHostedService : IHostedService
{
    private readonly DiscordSocketClient _client;
    private readonly InteractionService _interactions;
    private readonly IServiceProvider _services;
    private readonly BotConfig _config;

    public BotHostedService(DiscordSocketClient client, InteractionService interactions, IServiceProvider services, BotConfig config)
    {
        _client = client;
        _interactions = interactions;
        _services = services;
        _config = config;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _client.Log += LogAsync;
        _interactions.Log += LogAsync;
        _client.Ready += ReadyAsync;
        _client.InteractionCreated += HandleInteractionAsync;

        await _interactions.AddModulesAsync(Assembly.GetEntryAssembly(), _services);

        await _client.LoginAsync(TokenType.Bot, _config.Token);
        await _client.StartAsync();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _client.StopAsync();
        await _client.LogoutAsync();
    }

    private async Task ReadyAsync()
    {
        if (_config.GuildId is ulong guildId)
        {
            await _interactions.RegisterCommandsToGuildAsync(guildId);
            Console.WriteLine($"[Bot] Logged in as {_client.CurrentUser}. Slash commands registered to guild {guildId} (instant).");
        }
        else
        {
            await _interactions.RegisterCommandsGloballyAsync();
            Console.WriteLine($"[Bot] Logged in as {_client.CurrentUser}. Slash commands registered globally (can take up to an hour to appear the first time).");
        }
    }

    private async Task HandleInteractionAsync(SocketInteraction interaction)
    {
        try
        {
            var context = new SocketInteractionContext(_client, interaction);
            await _interactions.ExecuteCommandAsync(context, _services);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Bot] Error handling interaction: {ex}");
        }
    }

    private Task LogAsync(LogMessage log)
    {
        Console.WriteLine(log.ToString());
        return Task.CompletedTask;
    }
}
