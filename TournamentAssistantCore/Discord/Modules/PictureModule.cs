using Discord;
using Discord.Interactions;
using System.IO;
using System.Threading.Tasks;
using TournamentAssistantCore.Discord.Services;

namespace TournamentAssistantCore.Discord.Modules
{
    public class PictureModule : InteractionModuleBase
    {
#if RELEASE
        public PictureService PictureService { get; set; }

        [SlashCommand("cat", "Gets a random cat picture")]
        public async Task CatAsync()
        {
            var stream = await PictureService.GetCatPictureAsync();
            stream.Seek(0, SeekOrigin.Begin);
            await Context.Channel.SendFileAsync(stream, "cat.png");
        }

        [SlashCommand("neko", "Gets a random neko picture")]
        public async Task NekoAsync()
        {
            var stream = await PictureService.GetNekoStreamAsync(PictureService.NekoType.Neko);
            stream.Seek(0, SeekOrigin.Begin);
            await Context.Channel.SendFileAsync(stream, "neko.png");
        }
        
        [SlashCommand("nekogif", "Gets a random neko gif")]
        public async Task NekoGifAsync()
        {
            var gifLink = await PictureService.GetNekoGifAsync();

            var builder = new EmbedBuilder();
            builder.WithImageUrl(gifLink);

            await ReplyAsync("", false, builder.Build());
        }

        [SlashCommand("nekolewd", "Gets a random lewd neko picture")]
        [RequireNsfw]
        public async Task NekoLewdAsync()
        {
            var stream = await PictureService.GetNekoStreamAsync(PictureService.NekoType.NekoLewd);
            stream.Seek(0, SeekOrigin.Begin);
            await Context.Channel.SendFileAsync(stream, "nekolewd.png");
        }

        [SlashCommand("nekolewdgif", "Gets a random lewd neko gif")]
        [RequireNsfw]
        public async Task NekoLewdGifAsync()
        {
            var gifLink = await PictureService.GetNekoLewdGifAsync();

            var builder = new EmbedBuilder();
            builder.WithImageUrl(gifLink);

            await ReplyAsync("", false, builder.Build());
        }

        [SlashCommand("lewd", "Gets a random lewd picture")]
        [RequireNsfw]
        public async Task LewdAsync()
        {
            var stream = await PictureService.GetNekoStreamAsync(PictureService.NekoType.Hentai);
            stream.Seek(0, SeekOrigin.Begin);
            await Context.Channel.SendFileAsync(stream, "lewd.png");
        }

        [SlashCommand("lewdgif", "Gets a random lewd gif")]
        [RequireNsfw]
        public async Task LewdGifAsync()
        {
            var gifLink = await PictureService.GetLewdGifAsync();

            var builder = new EmbedBuilder();
            builder.WithImageUrl(gifLink);

            await ReplyAsync("", false, builder.Build());
        }
#endif
    }
}
