using Microsoft.EntityFrameworkCore.Migrations;

namespace TournamentAssistantServer.Migrations.QualifierDatabase
{
    public partial class AddTargetScore : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Target",
                table: "QualifierSongs",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Target",
                table: "QualifierSongs");
        }
    }
}
