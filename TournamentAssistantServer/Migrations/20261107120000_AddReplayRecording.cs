using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TournamentAssistantServer.Database.Contexts;

namespace TournamentAssistantServer.Migrations
{
    [DbContext(typeof(TournamentDatabaseContext))]
    [Migration("20261107120000_AddReplayRecording")]
    public class AddReplayRecording : Migration
    {
        private static readonly string[] NewPermissions =
        {
            "tournament:settings:set_enable_replay_recording",
            "tournament:settings:set_replay_retention",
            "tournament:replays:download",
            "tournament:replays:remove",
        };

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EnableReplayRecording",
                table: "Tournaments",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ReplayRetention",
                table: "Tournaments",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "UpdatedAt",
                table: "Tournaments",
                nullable: false,
                defaultValue: 0L);

            // Existing tournaments count as updated now, so none of them get hidden right after this migration
            migrationBuilder.Sql("UPDATE Tournaments SET UpdatedAt = CAST((julianday('now') - 2440587.5) * 86400000 AS INTEGER);");

            foreach (var permission in NewPermissions)
            {
                migrationBuilder.Sql(
                    $"UPDATE Roles SET Permissions = Permissions || ',{permission}' "
                        + $"WHERE RoleId IN ('admin', 'bks-admin') AND Permissions NOT LIKE '%{permission}%';"
                );
            }
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "EnableReplayRecording", table: "Tournaments");
            migrationBuilder.DropColumn(name: "ReplayRetention", table: "Tournaments");
            migrationBuilder.DropColumn(name: "UpdatedAt", table: "Tournaments");
        }
    }
}
