using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TournamentAssistantDiscordBot.Discord.Services;
using TournamentAssistantShared;
using TournamentAssistantShared.Models;

/**
 * Created by Moon on 5/18/2019
 * A Discord.NET module for basic bot functionality, not necessarily relating to Beat Saber
 */

namespace TournamentAssistantDiscordBot.Discord.Modules
{
    enum Permissions
    {
        None = 0,
        View = 1,
        Admin = 2
    };

    public class GenericModule : InteractionModuleBase<SocketInteractionContext>
    {
        public TAInteractionService TAInteractionService { get; set; }

        public class OngoingInteractionInfo
        {
            public string TournamentId;
            public List<IMentionable> Roles { get; set; } = new List<IMentionable>();
            public string AccessType { get; set; } = string.Empty;
        }

        // Ongoing interactions, keyed by userId
        public static Dictionary<string, OngoingInteractionInfo> OngoingInteractions = new Dictionary<string, OngoingInteractionInfo>();

        [SlashCommand("authorize", "Authorize users in your tournament")]
        public async Task Authorize()
        {
            await RespondAsync("If you're reading this, yell at me (@matrikmoon) to fix this properly. It used to work, but I'm currently zooming through adding customizable roles and don't have time to fix the bot side of things (I'm fairly sure no one uses this anyway). Please let me know if you want to (because believe me I know how convenient it is) and I can fix it in an hour or two. o7", ephemeral: true);

            /*var tournaments = await TAInteractionService.GetTournamentsWhereUserIsAdmin(Context.User.Id.ToString());

            if (tournaments.Count == 0)
            {
                await RespondAsync("You are not Admin of any tournament", ephemeral: true);
            }
            else if (tournaments.Count == 1)
            {
                OngoingInteractions.Add(Context.User.Id.ToString(), new OngoingInteractionInfo
                {
                    TournamentId = tournaments.First().Guid
                });
            }
            else
            {
                OngoingInteractions.Add(Context.User.Id.ToString(), new OngoingInteractionInfo());
            }

            await RespondAsync(components: BuildComponents(OngoingInteractions[Context.User.Id.ToString()], tournaments), ephemeral: true);*/
        }

        [ComponentInteraction("add_button")]
        public async Task AddButtonInteracted()
        {
            if (!OngoingInteractions.TryGetValue(Context.User.Id.ToString(), out var ongoingInteractionInfo))
            {
                await RespondAsync("Interaction has expired! Try running `/authorize` again", ephemeral: true);
            }
            else if (ongoingInteractionInfo.TournamentId == null || ongoingInteractionInfo.Roles.Count < 1 || string.IsNullOrWhiteSpace(ongoingInteractionInfo.AccessType))
            {
                await RespondAsync("Please select a Tournament, a user/role to add, and an access level", ephemeral: true);
            }
            else
            {
                void addUserPerm(string userId)
                {
                    // TODO: We should enumerate available roles and display them here.
                    // But for now...
                    var permissions = Permissions.None;
                    switch (ongoingInteractionInfo.AccessType)
                    {
                        case "view":
                            permissions = Permissions.View;
                            break;
                        case "admin":
                            permissions = Permissions.View | Permissions.Admin;
                            break;
                        default:
                            permissions = Permissions.None;
                            break;
                    }

                    // Yep. You're welcome, future me.

                    // TAInteractionService.AddAuthorizedUser(ongoingInteractionInfo.TournamentId, userId, permissions);
                }

                foreach (var userOrRole in ongoingInteractionInfo.Roles)
                {
                    Logger.Debug(userOrRole.GetType().ToString());

                    if (userOrRole is SocketGuildUser user)
                    {
                        Logger.Debug($"Adding: {user.DisplayName}");
                        addUserPerm(user.Id.ToString());
                    }
                    else if (userOrRole is SocketRole role)
                    {
                        // TODO: I really hope no one runs this in the BSMG
                        var allUsers = await Context.Guild.GetUsersAsync().FlattenAsync();
                        foreach (var roleUser in allUsers.Where(x => x.RoleIds.Contains(role.Id)))
                        {
                            Logger.Debug($"Adding: {roleUser.DisplayName}");
                            addUserPerm(roleUser.Id.ToString());
                        }
                    }
                }

                OngoingInteractions.Remove(Context.User.Id.ToString());

                await DeferAsync();
                await Context.Interaction.ModifyOriginalResponseAsync((original) =>
                {
                    original.Content = $"Success! {Emoji.Parse(":white_check_mark:")}";
                    original.Components = null;
                });
            }
        }

        [ComponentInteraction("tournament_id_select")]
        public async Task TournamentIdSelectInteracted(string[] tournamentId)
        {
            if (!OngoingInteractions.TryGetValue(Context.User.Id.ToString(), out var ongoingInteractionInfo))
            {
                await RespondAsync("Interaction has expired! Try running `/authorize` again", ephemeral: true);
            }
            else
            {
                ongoingInteractionInfo.TournamentId = tournamentId.FirstOrDefault();

                var tournaments = await TAInteractionService.GetTournamentsWhereUserIsAdmin(Context.User.Id.ToString());

                await DeferAsync();
                await Context.Interaction.ModifyOriginalResponseAsync((messageProperties) => UpdateComponentVisibility(ongoingInteractionInfo, messageProperties, tournaments));
            }
        }

        [ComponentInteraction("mentionable_select")]
        public async Task MentionableSelectInteracted(IMentionable[] selectedMentionables)
        {
            if (!OngoingInteractions.TryGetValue(Context.User.Id.ToString(), out var ongoingInteractionInfo))
            {
                await RespondAsync("Interaction has expired! Try running `/authorize` again", ephemeral: true);
            }
            else
            {
                ongoingInteractionInfo.Roles.Clear();
                ongoingInteractionInfo.Roles.AddRange(selectedMentionables);

                var tournaments = await TAInteractionService.GetTournamentsWhereUserIsAdmin(Context.User.Id.ToString());

                await DeferAsync();
                await Context.Interaction.ModifyOriginalResponseAsync((messageProperties) => UpdateComponentVisibility(ongoingInteractionInfo, messageProperties, tournaments));
            }
        }

        [ComponentInteraction("access_type_select")]
        public async Task AccessTypeSelectInteracted(string[] accessType)
        {
            if (!OngoingInteractions.TryGetValue(Context.User.Id.ToString(), out var ongoingInteractionInfo))
            {
                await RespondAsync("Interaction has expired! Try running `/authorize` again", ephemeral: true);
            }
            else
            {
                ongoingInteractionInfo.AccessType = accessType.FirstOrDefault();

                var tournaments = await TAInteractionService.GetTournamentsWhereUserIsAdmin(Context.User.Id.ToString());

                await DeferAsync();
                await Context.Interaction.ModifyOriginalResponseAsync((messageProperties) => UpdateComponentVisibility(ongoingInteractionInfo, messageProperties, tournaments));
            }
        }

        private MessageComponent BuildComponents(OngoingInteractionInfo ongoingInteractionInfo, List<Tournament> tournamentsWhereUserIsAdmin)
        {
            var showAccessTypeSelect = ongoingInteractionInfo.Roles.Count > 0;
            var showAddButton = ongoingInteractionInfo.Roles.Count > 0 && !string.IsNullOrWhiteSpace(ongoingInteractionInfo.AccessType);

            var tournamentSelectBuilder = new SelectMenuBuilder()
                .WithCustomId("tournament_id_select")
                .WithMinValues(1)
                .WithMaxValues(1)
                .WithPlaceholder("Select tournament for which to authorize these users");

            if (tournamentsWhereUserIsAdmin.Count == 0)
            {
                return null;
            }

            var options = tournamentsWhereUserIsAdmin.Select(x => new SelectMenuOptionBuilder().WithLabel(x.Settings.TournamentName).WithValue(x.Guid)).ToList();
            tournamentSelectBuilder = tournamentSelectBuilder.WithOptions(options);

            if (tournamentsWhereUserIsAdmin.Count == 1)
            {
                tournamentSelectBuilder.Options.First().IsDefault = true;
                tournamentSelectBuilder = tournamentSelectBuilder.WithDisabled(true);
            }
            else if (ongoingInteractionInfo.TournamentId != null)
            {
                tournamentSelectBuilder.Options.First(x => x.Value == ongoingInteractionInfo.TournamentId).IsDefault = true;
                tournamentSelectBuilder = tournamentSelectBuilder.WithDisabled(true);
            }

            var mentionableSelectBuilder = new SelectMenuBuilder()
                .WithCustomId("mentionable_select")
                .WithType(ComponentType.MentionableSelect)
                .WithMinValues(1)
                .WithMaxValues(25)
                .WithPlaceholder("Select a user or role")
                .WithDisabled(ongoingInteractionInfo.TournamentId == null);

            var accessTypeSelectBuilder = new SelectMenuBuilder()
                .WithCustomId("access_type_select")
                .AddOption("View / Participate", "view", isDefault: "view" == ongoingInteractionInfo.AccessType)
                .AddOption("Admin", "admin", isDefault: "admin" == ongoingInteractionInfo.AccessType)
                .WithPlaceholder("Select a level of access")
                .WithDisabled(!showAccessTypeSelect);

            var buttonBuilder = new ButtonBuilder()
                .WithCustomId("add_button")
                .WithLabel("Add")
                .WithStyle(ButtonStyle.Success)
                .WithDisabled(!showAddButton);

            return new ComponentBuilder()
                .WithSelectMenu(tournamentSelectBuilder)
                .WithSelectMenu(mentionableSelectBuilder)
                .WithSelectMenu(accessTypeSelectBuilder)
                .WithButton(buttonBuilder)
                .Build();
        }

        private void UpdateComponentVisibility(OngoingInteractionInfo ongoingInteractionInfo, MessageProperties messageProperties, List<Tournament> tournamentsWhereUserIsAdmin)
        {
            messageProperties.Components = BuildComponents(ongoingInteractionInfo, tournamentsWhereUserIsAdmin);
        }
    }
}
