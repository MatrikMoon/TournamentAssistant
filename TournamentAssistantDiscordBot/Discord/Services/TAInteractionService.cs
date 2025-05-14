
using System;
using System.Collections.Generic;
using System.Security;
using System.Threading.Tasks;
using TournamentAssistantShared.Models;
using static TournamentAssistantDiscordBot.Discord.Modules.GenericModule;

/**
* Created by Moon on 9/13/2020
* I can't seem to use SystemServer directly in DI
* because if it *doesn't* exist, DI throws a fit.
* So I'm using this intermediary class to serve
* it if it does exist.
* 
* Modified 5/11/2025
* I don't seem to use this anymore, and now Discord.NET
* is causing dependency conflicts with ASP. This will now
* host callbacks so that the newly-split Discord library
* can request info from the TA Server
*/
namespace TournamentAssistantDiscordBot.Discord.Services
{
    public class TAInteractionService
    {
        private Func<string, Task<List<Tournament>>> _getTournamentsWhereUserIsAdmin;
        private Action<string, string, Permissions> _addAuthorizedUser;

        public TAInteractionService(Func<string, Task<List<Tournament>>> getTournamentsWhereUserIsAdmin, Action<string, string, Permissions> addAuthorizedUser)
        {
            _getTournamentsWhereUserIsAdmin = getTournamentsWhereUserIsAdmin;
            _addAuthorizedUser = addAuthorizedUser;
        }

        public async Task<List<Tournament>> GetTournamentsWhereUserIsAdmin(string userId)
        {
            return await _getTournamentsWhereUserIsAdmin(userId);
        }

        public void AddAuthorizedUser(string tournamentId, string userId, Permissions permissions)
        {
            _addAuthorizedUser(tournamentId, userId, permissions);
        }
    }
}
