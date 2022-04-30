using Discord;
using Discord.Commands;
using System.IO;
using System.Threading.Tasks;
using TournamentAssistantCore.Discord.Services;

namespace TournamentAssistantCore.Discord.Modules
{
    public class PictureModule : ModuleBase<SocketCommandContext>
    {
        public PictureService PictureService { get; set; }

        [Command("cat")]
        public async Task CatAsync()
        {
            var stream = await PictureService.GetCatPictureAsync();
            stream.Seek(0, SeekOrigin.Begin);
            await Context.Channel.SendFileAsync(stream, "cat.png");
        }

        [Command("neko")]
        public async Task NekoAsync()
        {
            var stream = await PictureService.GetNekoStreamAsync(PictureService.NekoType.Neko);
            stream.Seek(0, SeekOrigin.Begin);
            await Context.Channel.SendFileAsync(stream, "neko.png");
        }

        [Command("nekolewd")]
        [RequireNsfw]
        public async Task NekoLewdAsync()
        {
            var stream = await PictureService.GetNekoStreamAsync(PictureService.NekoType.NekoLewd);
            stream.Seek(0, SeekOrigin.Begin);
            await Context.Channel.SendFileAsync(stream, "nekolewd.png");
        }

        [Command("nekogif")]
        public async Task NekoGifAsync()
        {
            var gifLink = await PictureService.GetNekoGifAsync();

            var builder = new EmbedBuilder();
            builder.WithImageUrl(gifLink);

            await ReplyAsync("", false, builder.Build());
        }

        [Command("nekolewdgif")]
        [RequireNsfw]
        public async Task NekoLewdGifAsync()
        {
            var gifLink = await PictureService.GetNekoLewdGifAsync();

            var builder = new EmbedBuilder();
            builder.WithImageUrl(gifLink);

            await ReplyAsync("", false, builder.Build());
        }

        [Command("lewd")]
        [RequireNsfw]
        public async Task LewdAsync()
        {
            var stream = await PictureService.GetNekoStreamAsync(PictureService.NekoType.Hentai);
            stream.Seek(0, SeekOrigin.Begin);
            await Context.Channel.SendFileAsync(stream, "lewd.png");
        }

        [Command("lewdgif")]
        [RequireNsfw]
        public async Task LewdGifAsync()
        {
            var gifLink = await PictureService.GetLewdGifAsync();

            var builder = new EmbedBuilder();
            builder.WithImageUrl(gifLink);

            await ReplyAsync("", false, builder.Build());
        }
    }
}
