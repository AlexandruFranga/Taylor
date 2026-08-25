using Discord;
using Discord.Interactions;
using WorkTimeBot.Services;

namespace WorkTimeBot.Modules;

public class SettingsModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly TrackingStore _store;

    public SettingsModule(TrackingStore store)
    {
        _store = store;
    }

    [SlashCommand("set-channel", "Set the channel where the weekly top 3 leaderboard gets posted automatically.")]
    [RequireContext(ContextType.Guild)]
    [RequireUserPermission(GuildPermission.ManageGuild)]
    public async Task SetWeeklyChannelAsync(
        [Summary("channel", "Channel to post the weekly leaderboard in")] ITextChannel channel)
    {
        await _store.SetWeeklyChannelIdAsync(channel.Id);
        await RespondAsync($"✅ Weekly top 3 leaderboard will now post in {channel.Mention} every week.", ephemeral: true);
    }
}
