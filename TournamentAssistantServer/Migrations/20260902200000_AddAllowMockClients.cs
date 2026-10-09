using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TournamentAssistantServer.Database.Contexts;

namespace TournamentAssistantServer.Migrations
{
    [DbContext(typeof(TournamentDatabaseContext))]
    [Migration("20260902200000_AddAllowMockClients")]
    public class AddAllowMockClients : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowMockClients",
                table: "Tournaments",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(
                "UPDATE Roles SET Permissions = Permissions || ',tournament:settings:set_allow_mock_clients' " +
                "WHERE RoleId = 'admin' AND Permissions NOT LIKE '%tournament:settings:set_allow_mock_clients%';");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "AllowMockClients", table: "Tournaments");
        }
    }
}
