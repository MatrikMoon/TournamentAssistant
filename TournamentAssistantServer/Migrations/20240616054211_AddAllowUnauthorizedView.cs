using Microsoft.EntityFrameworkCore.Migrations;

namespace TournamentAssistantServer.Migrations
{
    public partial class AddAllowUnauthorizedView : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowUnauthorizedView",
                table: "Tournaments",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowUnauthorizedView",
                table: "Tournaments");
        }
    }
}
