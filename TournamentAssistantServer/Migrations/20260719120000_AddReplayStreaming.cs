using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TournamentAssistantServer.Database.Contexts;

namespace TournamentAssistantServer.Migrations
{
    [DbContext(typeof(TournamentDatabaseContext))]
    [Migration("20260719120000_AddReplayStreaming")]
    public class AddReplayStreaming : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EnableReplayStreaming",
                table: "Tournaments",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(
                "UPDATE Roles SET Permissions = Permissions || ',tournament:settings:set_enable_replay_streaming' " +
                "WHERE RoleId = 'admin' AND Permissions NOT LIKE '%tournament:settings:set_enable_replay_streaming%';");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "EnableReplayStreaming", table: "Tournaments");
        }
    }
}
