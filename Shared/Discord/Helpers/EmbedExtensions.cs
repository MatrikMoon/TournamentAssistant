using Discord;

/**
 * Created by Moon on 9/3/2020, 4:25AM
 * Quick extension class so we can build basic embeds from strings
 */

namespace TournamentAssistantShared.Discord.Helpers
{
    public static class EmbedExtensions
    {
        public static Embed SuccessEmbed(this string text)
        {
            var builder = new EmbedBuilder();
            builder.Title = "<:white_check_mark:751010797781778482> Success";
            builder.Color = Color.DarkGreen;

            builder.AddField("Success", text);

            return builder.Build();
        }

        public static Embed InfoEmbed(this string text)
        {
            var builder = new EmbedBuilder();
            builder.Title = "<:page_with_curl:735592941338361897> Info";
            builder.Color = Color.Blue;

            builder.AddField("Info", text);

            return builder.Build();
        }

        public static Embed WarningEmbed(this string text)
        {
            var builder = new EmbedBuilder();
            builder.Title = "<:warning:751009530170703882> Warning";
            builder.Color = Color.Gold;

            builder.AddField("Warning", text);

            return builder.Build();
        }

        public static Embed ErrorEmbed(this string text)
        {
            var builder = new EmbedBuilder();
            builder.Title = "<:octagonal_sign:751009755404959865> Error";
            builder.Color = Color.Red;

            builder.AddField("Error", text);

            return builder.Build();
        }
    }
}
