using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TournamentAssistantServer.Database.Contexts;

namespace TournamentAssistantServer.Migrations.QualifierDatabase
{
    [DbContext(typeof(QualifierDatabaseContext))]
    [Migration("20260902190000_AddQualifierSchedule")]
    public class AddQualifierSchedule : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "StartTimeUnixSeconds",
                table: "Qualifiers",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "EndTimeUnixSeconds",
                table: "Qualifiers",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "StartTimeUnixSeconds", table: "Qualifiers");
            migrationBuilder.DropColumn(name: "EndTimeUnixSeconds", table: "Qualifiers");
        }
    }
}
