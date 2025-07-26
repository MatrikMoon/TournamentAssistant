using System;
using System.Collections.Generic;
using System.Linq;

namespace TournamentAssistantShared
{
    public class Permissions
    {
        public static class PermissionValues
        {
            public const string ViewTournamentInList = "tournament:view_tournament_in_list";
            public const string JoinTournament = "tournament:join";

            public const string AddAuthorizedUsers = "tournament:settings:add_authorized_users";
            public const string UpdateAuthorizedUserRoles = "tournament:settings:update_authorized_user_roles";
            public const string RemoveAuthorizedUsers = "tournament:settings:remove_authorized_users";
            public const string GetAuthorizedUsers = "tournament:settings:get_authorized_users";
            public const string GetDiscordInfo = "tournament:settings:get_discord_info";

            public const string GetQualifierScores = "tournament:qualifier:get_qualifier_scores";
            public const string SubmitQualifierScores = "tournament:qualifier:submit_qualifier_scores";
            public const string SeeHiddenQualifierScores = "tournament:qualifier:see_hidden_qualifier_scores";
            public const string GetRemainingAttempts = "tournament:qualifier:get_remaining_attempts";
            public const string RefundAttempts = "tournament:qualifier:refund_attempts";

            public const string ReturnToMenu = "tournament:player:return_to_menu";
            public const string PlaySong = "tournament:player:play_song";
            public const string PlayWithStreamSync = "tournament:player:play_with_stream_sync";
            public const string ModifyGameplay = "tournament:player:modify_gameplay";
            public const string LoadSong = "tournament:player:load_song";

            public const string CreateMatch = "tournament:match:create_match";
            public const string AddUserToMatch = "tournament:match:add_user_to_match";
            public const string RemoveUserFromMatch = "tournament:match:remove_user_from_match";
            public const string SetMatchLeader = "tournament:match:set_match_leader";
            public const string SetMatchMap = "tournament:match:set_match_map";
            public const string DeleteMatch = "tournament:match:delete_match";

            public const string CreateQualifier = "tournament:qualifier:create";
            public const string SetQualifierName = "tournament:qualifier:set_name";
            public const string SetQualifierImage = "tournament:qualifier:set_image";
            public const string SetQualifierInfoChannel = "tournament:qualifier:set_info_channel";
            public const string SetQualifierFlags = "tournament:qualifier:set_flags";
            public const string SetQualifierLeaderboardSort = "tournament:qualifier:set_leaderboard_sort";
            public const string AddQualifierMaps = "tournament:qualifier:add_maps";
            public const string UpdateQualifierMap = "tournament:qualifier:update_map";
            public const string RemoveQualifierMap = "tournament:qualifier:remove_map";
            public const string DeleteQualifier = "tournament:qualifier:delete";

            public const string SetTournamentName = "tournament:settings:set_name";
            public const string SetTournamentImage = "tournament:settings:set_image";
            public const string SetTournamentEnableTeams = "tournament:settings:set_enable_teams";
            public const string SetTournamentEnablePools = "tournament:settings:set_enable_pools";
            public const string SetTournamentShowTournamentButton = "tournament:settings:set_show_tournament_button";
            public const string SetTournamentShowQualifierButton = "tournament:settings:set_show_qualifier_button";
            public const string SetTournamentAllowUnauthorizedView = "tournament:settings:set_allow_unauthorized_view";
            public const string SetTournamentScoreUpdateFrequency = "tournament:settings:set_score_update_frequency";
            public const string SetTournamentBannedMods = "tournament:settings:set_banned_mods";
            public const string AddTournamentTeam = "tournament:settings:add_team";
            public const string SetTournamentTeamName = "tournament:settings:set_team_name";
            public const string SetTournamentTeamImage = "tournament:settings:set_team_image";
            public const string RemoveTournamentTeam = "tournament:settings:remove_team";
            public const string AddTournamentPool = "tournament:settings:add_pool";
            public const string SetTournamentPoolName = "tournament:settings:set_pool_name";
            public const string SetTournamentPoolImage = "tournament:settings:set_pool_image";
            public const string AddTournamentPoolMaps = "tournament:settings:add_pool_maps";
            public const string UpdateTournamentPoolMaps = "tournament:settings:update_pool_maps";
            public const string RemoveTournamentPoolMaps = "tournament:settings:remove_pool_maps";
            public const string RemoveTournamentPools = "tournament:settings:remove_pools";
            public const string AddTournamentRole = "tournament:settings:add_role";
            public const string SetTournamentRoleName = "tournament:settings:set_role_name";
            public const string SetTournamentRolePermissions = "tournament:settings:set_role_permissions";
            public const string RemoveTournamentRole = "tournament:settings:remove_role";
            public const string DeleteTournament = "tournament:settings:delete";
        }

        private static readonly Dictionary<string, Permissions> _allPermissions = new();

        public static readonly Permissions ViewTournamentInList = Register(PermissionValues.ViewTournamentInList);
        public static readonly Permissions JoinTournament = Register(PermissionValues.JoinTournament);
        public static readonly Permissions AddAuthorizedUsers = Register(PermissionValues.AddAuthorizedUsers);
        public static readonly Permissions UpdateAuthorizedUserRoles = Register(PermissionValues.UpdateAuthorizedUserRoles);
        public static readonly Permissions RemoveAuthorizedUsers = Register(PermissionValues.RemoveAuthorizedUsers);
        public static readonly Permissions GetAuthorizedUsers = Register(PermissionValues.GetAuthorizedUsers);
        public static readonly Permissions GetDiscordInfo = Register(PermissionValues.GetDiscordInfo);

        public static readonly Permissions GetQualifierScores = Register(PermissionValues.GetQualifierScores);
        public static readonly Permissions SubmitQualifierScores = Register(PermissionValues.SubmitQualifierScores);
        public static readonly Permissions SeeHiddenQualifierScores = Register(PermissionValues.SeeHiddenQualifierScores);
        public static readonly Permissions GetRemainingAttempts = Register(PermissionValues.GetRemainingAttempts);
        public static readonly Permissions RefundAttempts = Register(PermissionValues.RefundAttempts);

        public static readonly Permissions ReturnToMenu = Register(PermissionValues.ReturnToMenu);
        public static readonly Permissions PlaySong = Register(PermissionValues.PlaySong);
        public static readonly Permissions PlayWithStreamSync = Register(PermissionValues.PlayWithStreamSync);
        public static readonly Permissions ModifyGameplay = Register(PermissionValues.ModifyGameplay);
        public static readonly Permissions LoadSong = Register(PermissionValues.LoadSong);

        public static readonly Permissions CreateMatch = Register(PermissionValues.CreateMatch);
        public static readonly Permissions AddUserToMatch = Register(PermissionValues.AddUserToMatch);
        public static readonly Permissions RemoveUserFromMatch = Register(PermissionValues.RemoveUserFromMatch);
        public static readonly Permissions SetMatchLeader = Register(PermissionValues.SetMatchLeader);
        public static readonly Permissions SetMatchMap = Register(PermissionValues.SetMatchMap);
        public static readonly Permissions DeleteMatch = Register(PermissionValues.DeleteMatch);

        public static readonly Permissions CreateQualifier = Register(PermissionValues.CreateQualifier);
        public static readonly Permissions SetQualifierName = Register(PermissionValues.SetQualifierName);
        public static readonly Permissions SetQualifierImage = Register(PermissionValues.SetQualifierImage);
        public static readonly Permissions SetQualifierInfoChannel = Register(PermissionValues.SetQualifierInfoChannel);
        public static readonly Permissions SetQualifierFlags = Register(PermissionValues.SetQualifierFlags);
        public static readonly Permissions SetQualifierLeaderboardSort = Register(PermissionValues.SetQualifierLeaderboardSort);
        public static readonly Permissions AddQualifierMaps = Register(PermissionValues.AddQualifierMaps);
        public static readonly Permissions UpdateQualifierMap = Register(PermissionValues.UpdateQualifierMap);
        public static readonly Permissions RemoveQualifierMap = Register(PermissionValues.RemoveQualifierMap);
        public static readonly Permissions DeleteQualifier = Register(PermissionValues.DeleteQualifier);

        public static readonly Permissions SetTournamentName = Register(PermissionValues.SetTournamentName);
        public static readonly Permissions SetTournamentImage = Register(PermissionValues.SetTournamentImage);
        public static readonly Permissions SetTournamentEnableTeams = Register(PermissionValues.SetTournamentEnableTeams);
        public static readonly Permissions SetTournamentEnablePools = Register(PermissionValues.SetTournamentEnablePools);
        public static readonly Permissions SetTournamentShowTournamentButton = Register(PermissionValues.SetTournamentShowTournamentButton);
        public static readonly Permissions SetTournamentShowQualifierButton = Register(PermissionValues.SetTournamentShowQualifierButton);
        public static readonly Permissions SetTournamentAllowUnauthorizedView = Register(PermissionValues.SetTournamentAllowUnauthorizedView);
        public static readonly Permissions SetTournamentScoreUpdateFrequency = Register(PermissionValues.SetTournamentScoreUpdateFrequency);
        public static readonly Permissions SetTournamentBannedMods = Register(PermissionValues.SetTournamentBannedMods);
        public static readonly Permissions AddTournamentTeam = Register(PermissionValues.AddTournamentTeam);
        public static readonly Permissions SetTournamentTeamName = Register(PermissionValues.SetTournamentTeamName);
        public static readonly Permissions SetTournamentTeamImage = Register(PermissionValues.SetTournamentTeamImage);
        public static readonly Permissions RemoveTournamentTeam = Register(PermissionValues.RemoveTournamentTeam);
        public static readonly Permissions AddTournamentPool = Register(PermissionValues.AddTournamentPool);
        public static readonly Permissions SetTournamentPoolName = Register(PermissionValues.SetTournamentPoolName);
        public static readonly Permissions SetTournamentPoolImage = Register(PermissionValues.SetTournamentPoolImage);
        public static readonly Permissions AddTournamentPoolMaps = Register(PermissionValues.AddTournamentPoolMaps);
        public static readonly Permissions UpdateTournamentPoolMaps = Register(PermissionValues.UpdateTournamentPoolMaps);
        public static readonly Permissions RemoveTournamentPoolMaps = Register(PermissionValues.RemoveTournamentPoolMaps);
        public static readonly Permissions RemoveTournamentPools = Register(PermissionValues.RemoveTournamentPools);
        public static readonly Permissions AddTournamentRole = Register(PermissionValues.AddTournamentRole);
        public static readonly Permissions SetTournamentRoleName = Register(PermissionValues.SetTournamentRoleName);
        public static readonly Permissions SetTournamentRolePermissions = Register(PermissionValues.SetTournamentRolePermissions);
        public static readonly Permissions RemoveTournamentRole = Register(PermissionValues.RemoveTournamentRole);
        public static readonly Permissions DeleteTournament = Register(PermissionValues.DeleteTournament);

        public string Value { get; }

        private Permissions(string value)
        {
            Value = value;
        }

        private static Permissions Register(string value)
        {
            var permission = new Permissions(value);
            _allPermissions[value] = permission;
            return permission;
        }
        public static Permissions FromValue(string value)
        {
            if (_allPermissions.TryGetValue(value, out var permission))
                return permission;

            throw new ArgumentException($"Unknown permission value: {value}");
        }

        public static List<Permissions> GetAllPermissions()
        {
            return _allPermissions.ToList().Select(x => x.Value).ToList();
        }

        public override string ToString() => Value;
        public override bool Equals(object obj) => obj is Permissions p && Value == p.Value;
        public override int GetHashCode() => Value.GetHashCode();

        public static implicit operator string(Permissions p) => p.Value;
        public static bool operator ==(Permissions a, Permissions b) => a?.Value == b?.Value;
        public static bool operator !=(Permissions a, Permissions b) => !(a == b);
    }
}
