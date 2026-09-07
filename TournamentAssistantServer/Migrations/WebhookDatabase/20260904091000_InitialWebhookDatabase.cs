using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TournamentAssistantServer.Database.Contexts;

namespace TournamentAssistantServer.Migrations.WebhookDatabase
{
    [DbContext(typeof(WebhookDatabaseContext))]
    [Migration("20260904091000_InitialWebhookDatabase")]
    public class InitialWebhookDatabase : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Webhooks",
                columns: table => new
                {
                    ID = table.Column<ulong>(nullable: false).Annotation("Sqlite:Autoincrement", true),
                    Guid = table.Column<string>(nullable: true),
                    TournamentId = table.Column<string>(nullable: true),
                    Url = table.Column<string>(nullable: true),
                    Triggers = table.Column<long>(nullable: false),
                    SigningSecret = table.Column<string>(nullable: true),
                    Old = table.Column<bool>(nullable: false),
                },
                constraints: table => table.PrimaryKey("PK_Webhooks", x => x.ID)
            );

            migrationBuilder.CreateIndex(
                name: "IX_Webhooks_Guid",
                table: "Webhooks",
                column: "Guid",
                unique: true
            );
            migrationBuilder.CreateIndex(
                name: "IX_Webhooks_TournamentId",
                table: "Webhooks",
                column: "TournamentId"
            );
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "Webhooks");
        }
    }
}
