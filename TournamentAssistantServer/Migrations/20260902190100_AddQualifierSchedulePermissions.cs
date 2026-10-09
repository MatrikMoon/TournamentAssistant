using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TournamentAssistantServer.Database.Contexts;

namespace TournamentAssistantServer.Migrations
{
    [DbContext(typeof(TournamentDatabaseContext))]
    [Migration("20260902190100_AddQualifierSchedulePermissions")]
    public class AddQualifierSchedulePermissions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE Roles SET Permissions = Permissions || ',tournament:qualifier:set_start_time,tournament:qualifier:set_end_time' " +
                "WHERE RoleId = 'admin' AND Permissions NOT LIKE '%tournament:qualifier:set_start_time%';");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
