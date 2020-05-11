#pragma warning disable 1998
using TournamentAssistantShared.Discord.Services;
using Discord;
using Discord.Commands;
using System.Collections.Generic;
using System.Threading.Tasks;

/**
 * Created by Moon on 5/18/2019
 * A Discord.NET module for basic bot functionality, not necessarily relating to Beat Saber
 */

namespace TournamentAssistantShared.Discord.Modules
{
    public class BotModule : ModuleBase<SocketCommandContext>
    {
        public MessageUpdateService MessageUpdateService { get; set; }
        public DatabaseService DatabaseService { get; set; }

        //Pull parameters out of an argument list string
        //Note: argument specifiers are required to start with "-"
        private static string ParseArgs(string argString, string argToGet)
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

        private bool IsAdmin()
        {
            return ((IGuildUser)Context.User).GuildPermissions.Has(GuildPermission.Administrator);
        }

        [Command("test")]
        [Summary("A test command, for quick access to test features")]
        public async Task Test([Remainder] string args = null)
        {
            if (IsAdmin())
            {
                
            }
        }
    }
}
