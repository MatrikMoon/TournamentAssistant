using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TournamentAssistantServer.Database.Contexts;

namespace TournamentAssistantServer.Migrations
{
    [DbContext(typeof(TournamentDatabaseContext))]
    [Migration("20260904090000_AddWebhookPermission")]
    public class AddWebhookPermission : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE Roles SET Permissions = Permissions || ',tournament:webhooks:manage' "
                    + "WHERE RoleId = 'admin' AND Permissions NOT LIKE '%tournament:webhooks:manage%';"
            );
        }

        protected override void Down(MigrationBuilder migrationBuilder) { }
    }
}
