using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TournamentAssistantServer.Database.Contexts;

namespace TournamentAssistantServer.Migrations
{
    [DbContext(typeof(TournamentDatabaseContext))]
    [Migration("20260918090000_AddBKTournaments")]
    public class AddBKTournaments : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsBKTournament",
                table: "Tournaments",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "IsBKTournament", table: "Tournaments");
        }
    }
}
