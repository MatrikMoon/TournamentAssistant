using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using TournamentAssistantServer.Utilities;
using UserDatabaseModel = TournamentAssistantServer.Database.Models.User;

namespace TournamentAssistantServer.Database.Contexts
{
    public class UserDatabaseContext : DatabaseContext
    {
        public UserDatabaseContext() : base("files/UserDatabase.db") { }

        public DbSet<UserDatabaseModel> Users { get; set; }

        public void AddUser(string token, string name, string userId, string ownerDiscordId)
        {
            // Remove existing user if applicable
            var existingUser = Users.FirstOrDefault(x => !x.Old && x.Guid == userId);
            if (existingUser != null)
            {
                existingUser.Old = true;
            }

            Users.Add(new UserDatabaseModel
            {
                Guid = userId,
                Name = name,
                OwnerDiscordId = ownerDiscordId,
                Token = token,
            });

            SaveChanges();
        }

        public UserDatabaseModel GetUser(string userId)
        {
            return Users.FirstOrDefault(x => !x.Old && x.Guid == userId);
        }

        public bool TokenExists(string token)
        {
            return Users.FirstOrDefault(x => !x.Old && x.Token == token) != null;
        }

        public List<UserDatabaseModel> GetTokensByOwner(string discordId)
        {
            if (discordId == "229408465787944970")
            {
                return Users.Where(x => !x.Old).ToList();
            }
            return Users.Where(x => !x.Old && x.OwnerDiscordId == discordId).ToList();
        }

        public void RevokeUser(string userId)
        {
            var existingUser = Users.FirstOrDefault(x => !x.Old && x.Guid == userId);
            existingUser.Old = true;

            SaveChanges();
        }
    }
}