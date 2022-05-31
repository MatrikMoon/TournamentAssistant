using System;
using Discord;
using System.Collections.Generic;
using System.Linq;

/**
 * Created by Moon on 9/3/2020, 4:25AM
 * Quick extension class so we can build basic embeds from strings
 */

namespace TournamentAssistantCore.Discord.Helpers
{
    public static class EmbedExtensions
    {
        public static Embed SuccessEmbed(this string text)
        {
            var builder = new EmbedBuilder
            {
                Title = "<:white_check_mark:751010797781778482> Success",
                Color = Color.DarkGreen
            };

            builder.AddField("Success", text);

            return builder.Build();
        }

        public static Embed InfoEmbed(this string text)
        {
            var builder = new EmbedBuilder
            {
                Title = "<:page_with_curl:735592941338361897> Info",
                Color = Color.Blue
            };

            builder.AddField("Info", text);

            return builder.Build();
        }

        public static Embed WarningEmbed(this string text)
        {
            var builder = new EmbedBuilder
            {
                Title = "<:warning:751009530170703882> Warning",
                Color = Color.Gold
            };

            builder.AddField("Warning", text);

            return builder.Build();
        }

        public static Embed ErrorEmbed(this string text)
        {
            var builder = new EmbedBuilder
            {
                Title = "<:octagonal_sign:751009755404959865> Error",
                Color = Color.Red
            };

            builder.AddField("Error", text);

            return builder.Build();
        }

        //Pull parameters out of an argument list string
        //Note: argument specifiers are required to start with "-"
        public static string ParseArgs(this string argString, string argToGet)
        {
            //Return nothing if the parameter arg string is empty
            if (string.IsNullOrWhiteSpace(argString) || string.IsNullOrWhiteSpace(argToGet)) return null;
            
            return argString.Split(' ').FirstOrDefault(t => string.Equals(t, $"{argToGet}", StringComparison.CurrentCultureIgnoreCase));
        }
    }
}
