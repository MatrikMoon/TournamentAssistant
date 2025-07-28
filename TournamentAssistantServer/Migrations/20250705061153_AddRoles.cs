using Microsoft.EntityFrameworkCore.Migrations;

namespace TournamentAssistantServer.Migrations
{
    public partial class AddRoles : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Rename the original table
            migrationBuilder.Sql("ALTER TABLE AuthorizedUsers RENAME TO _AuthorizedUsersOld;");

            // 2. Create the new AuthorizedUsers table WITHOUT PermissionFlags, WITH Roles
            migrationBuilder.Sql(@"
        CREATE TABLE AuthorizedUsers (
            ID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            Guid TEXT NOT NULL,
            TournamentId TEXT NOT NULL,
            DiscordId TEXT NOT NULL,
            Roles TEXT,
            Old INTEGER NOT NULL
        );
    ");

            // 3. Create a temporary table to keep PermissionFlags
            migrationBuilder.Sql(@"
        CREATE TEMP TABLE TempPermissions AS
        SELECT ID, TournamentId, PermissionFlags FROM _AuthorizedUsersOld;
    ");

            // 4. Copy user data to new table
            migrationBuilder.Sql(@"
        INSERT INTO AuthorizedUsers (ID, Guid, TournamentId, DiscordId, Roles, Old)
        SELECT ID, Guid, TournamentId, DiscordId, NULL, Old FROM _AuthorizedUsersOld;
    ");

            // 5. Drop old AuthorizedUsers table
            migrationBuilder.Sql("DROP TABLE _AuthorizedUsersOld;");

            // 6. Create Roles table
            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    ID = table.Column<ulong>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Guid = table.Column<string>(nullable: true),
                    Name = table.Column<string>(nullable: true),
                    RoleId = table.Column<string>(nullable: true),
                    TournamentId = table.Column<string>(nullable: true),
                    Permissions = table.Column<string>(nullable: true),
                    Old = table.Column<bool>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.ID);
                });

            // 7. Insert default roles for each tournament
            migrationBuilder.Sql(@"
        INSERT INTO Roles (Guid, Name, RoleId, TournamentId, Permissions, Old)
        SELECT lower(hex(randomblob(16))), 'Admin', 'admin', TournamentId, 'tournament:view_tournament_in_list,tournament:join,tournament:settings:add_authorized_users,tournament:settings:update_authorized_user_roles,tournament:settings:remove_authorized_users,tournament:settings:get_authorized_users,tournament:settings:get_discord_info,tournament:qualifier:get_qualifier_scores,tournament:qualifier:submit_qualifier_scores,tournament:qualifier:see_hidden_qualifier_scores,tournament:qualifier:get_remaining_attempts,tournament:qualifier:refund_attempts,tournament:player:return_to_menu,tournament:player:play_song,tournament:player:play_with_stream_sync,tournament:player:modify_gameplay,tournament:player:load_song,tournament:match:create_match,tournament:match:add_user_to_match,tournament:match:remove_user_from_match,tournament:match:set_match_leader,tournament:match:set_match_map,tournament:match:delete_match,tournament:qualifier:create,tournament:qualifier:set_name,tournament:qualifier:set_image,tournament:qualifier:set_info_channel,tournament:qualifier:set_flags,tournament:qualifier:set_leaderboard_sort,tournament:qualifier:add_maps,tournament:qualifier:update_map,tournament:qualifier:remove_map,tournament:qualifier:delete,tournament:settings:set_name,tournament:settings:set_image,tournament:settings:set_enable_teams,tournament:settings:set_enable_pools,tournament:settings:set_show_tournament_button,tournament:settings:set_show_qualifier_button,tournament:settings:set_allow_unauthorized_view,tournament:settings:set_score_update_frequency,tournament:settings:set_banned_mods,tournament:settings:add_team,tournament:settings:set_team_name,tournament:settings:set_team_image,tournament:settings:remove_team,tournament:settings:add_pool,tournament:settings:set_pool_name,tournament:settings:add_pool_maps,tournament:settings:update_pool_maps,tournament:settings:remove_pool_maps,tournament:settings:remove_pools,tournament:settings:add_role,tournament:settings:set_role_name,tournament:settings:set_role_permissions,tournament:settings:remove_role,tournament:settings:delete', 0 FROM (SELECT DISTINCT TournamentId FROM TempPermissions);
    ");
            migrationBuilder.Sql(@"
        INSERT INTO Roles (Guid, Name, RoleId, TournamentId, Permissions, Old)
        SELECT lower(hex(randomblob(16))), 'View Only', 'view_only', TournamentId, 'tournament:view_tournament_in_list,tournament:join', 0 FROM (SELECT DISTINCT TournamentId FROM TempPermissions);
    ");
            migrationBuilder.Sql(@"
        INSERT INTO Roles (Guid, Name, RoleId, TournamentId, Permissions, Old)
        SELECT lower(hex(randomblob(16))), 'Player', 'player', TournamentId, 'tournament:view_tournament_in_list,tournament:join,tournament:qualifier:get_qualifier_scores,tournament:qualifier:submit_qualifier_scores,tournament:qualifier:get_remaining_attempts', 0 FROM (SELECT DISTINCT TournamentId FROM TempPermissions);
    ");
            migrationBuilder.Sql(@"
        INSERT INTO Roles (Guid, Name, RoleId, TournamentId, Permissions, Old)
        SELECT lower(hex(randomblob(16))), 'Coordinator', 'coordinator', TournamentId, 'tournament:view_tournament_in_list,tournament:join,tournament:player:return_to_menu,tournament:player:play_song,tournament:player:play_with_stream_sync,tournament:player:modify_gameplay,tournament:player:load_song,tournament:match:create_match,tournament:match:add_user_to_match,tournament:match:remove_user_from_match,tournament:match:set_match_leader,tournament:match:set_match_map,tournament:match:delete_match', 0 FROM (SELECT DISTINCT TournamentId FROM TempPermissions);
    ");

            // 8. Assign roles to AuthorizedUsers based on old PermissionFlags
            migrationBuilder.Sql(@"
        UPDATE AuthorizedUsers
        SET Roles = 'admin'
        WHERE ID IN (
            SELECT ID FROM TempPermissions WHERE PermissionFlags = 2 OR PermissionFlags = 3
        );
    ");

            migrationBuilder.Sql(@"
        UPDATE AuthorizedUsers
        SET Roles = 'player,coordinator'
        WHERE ID IN (
            SELECT ID FROM TempPermissions WHERE PermissionFlags = 1
        );
    ");

            // 9. Drop the temp table
            migrationBuilder.Sql("DROP TABLE TempPermissions;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 1. Drop the Roles table
            migrationBuilder.DropTable(name: "Roles");

            // 2. Rename current AuthorizedUsers table
            migrationBuilder.Sql("ALTER TABLE AuthorizedUsers RENAME TO _AuthorizedUsersTemp;");

            // 3. Recreate original AuthorizedUsers table with PermissionFlags, without Roles
            migrationBuilder.Sql(@"
        CREATE TABLE AuthorizedUsers (
            ID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            Guid TEXT NOT NULL,
            TournamentId TEXT NOT NULL,
            DiscordId TEXT NOT NULL,
            PermissionFlags INTEGER NOT NULL DEFAULT 0,
            Old INTEGER NOT NULL
        );
    ");

            // 4. Copy data back, but PermissionFlags is set to 0
            migrationBuilder.Sql(@"
        INSERT INTO AuthorizedUsers (ID, Guid, TournamentId, DiscordId, PermissionFlags, Old)
        SELECT ID, Guid, TournamentId, DiscordId, 0, Old FROM _AuthorizedUsersTemp;
    ");

            // 5. Drop the temp table
            migrationBuilder.Sql("DROP TABLE _AuthorizedUsersTemp;");
        }
    }
}
