using Discord;
using System.Collections.Generic;

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

        //Pull parameters out of an argument list string
        //Note: argument specifiers are required to start with "-"
        public static string ParseArgs(this string argString, string argToGet)
        {
            //Return nothing if the parameter arg string is empty
            if (string.IsNullOrWhiteSpace(argString) || string.IsNullOrWhiteSpace(argToGet)) return null;

            List<string> argsWithQuotedStrings = new List<string>();
            string[] argArray = argString.Split(' ');

            for (int x = 0; x < argArray.Length; x++)
            {
                if (argArray[x].StartsWith("\""))
                {
                    string assembledString = string.Empty; //argArray[x].Substring(1) + " ";
                    for (int y = x; y < argArray.Length; y++)
                    {
                        if (argArray[y].StartsWith("\"")) argArray[y] = argArray[y].Substring(1); //Strip quotes off the front of the currently tested word.
                                                                                                  //This is necessary since this part of the code also handles the string right after the open quote
                        if (argArray[y].EndsWith("\""))
                        {
                            assembledString += argArray[y].Substring(0, argArray[y].Length - 1);
                            x = y;
                            break;
                        }
                        else assembledString += argArray[y] + " ";
                    }
                    argsWithQuotedStrings.Add(assembledString);
                }
                else argsWithQuotedStrings.Add(argArray[x]);
            }

            argArray = argsWithQuotedStrings.ToArray();

            for (int i = 0; i < argArray.Length; i++)
            {
                if (argArray[i].ToLower() == $"-{argToGet}".ToLower())
                {
                    if (((i + 1) < (argArray.Length)) && !argArray[i + 1].StartsWith("-"))
                    {
                        return argArray[i + 1];
                    }
                    else return "true";
                }
            }

            return null;
        }
    }
}
