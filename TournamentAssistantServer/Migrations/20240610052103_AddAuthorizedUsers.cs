using Microsoft.EntityFrameworkCore.Migrations;

namespace TournamentAssistantServer.Migrations
{
    public partial class AddAuthorizedUsers : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuthorizedUsers",
                columns: table => new
                {
                    ID = table.Column<ulong>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Guid = table.Column<string>(nullable: true),
                    TournamentId = table.Column<string>(nullable: true),
                    DiscordId = table.Column<string>(nullable: true),
                    PermissionFlags = table.Column<int>(nullable: false),
                    Old = table.Column<bool>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuthorizedUsers", x => x.ID);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuthorizedUsers");
        }
    }
}
