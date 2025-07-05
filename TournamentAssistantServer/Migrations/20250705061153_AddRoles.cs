using Microsoft.EntityFrameworkCore.Migrations;

namespace TournamentAssistantServer.Migrations
{
    public partial class AddRoles : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Rename the old table
            migrationBuilder.Sql("ALTER TABLE AuthorizedUsers RENAME TO _AuthorizedUsersOld;");

            // 2. Create the new AuthorizedUsers table WITHOUT 'PermissionFlags' and WITH 'Roles'
            migrationBuilder.Sql(@"
            CREATE TABLE AuthorizedUsers (
                ID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                Guid TEXT NOT NULL,
                TournamentId TEXT NOT NULL,
                DiscordId TEXT NOT NULL,
                Roles TEXT,
                Old INTEGER NOT NULL
            );
        ");

            // 3. Copy data over, omitting PermissionFlags and using NULL for the new Roles column
            migrationBuilder.Sql(@"
            INSERT INTO AuthorizedUsers (ID, Guid, TournamentId, DiscordId, Roles, Old)
            SELECT ID, Guid, TournamentId, DiscordId, NULL, Old FROM _AuthorizedUsersOld;
        ");

            // 4. Drop the old table
            migrationBuilder.Sql("DROP TABLE _AuthorizedUsersOld;");

            // 5. Create the new Roles table as originally defined
            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    ID = table.Column<ulong>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Guid = table.Column<string>(nullable: true),
                    Name = table.Column<string>(nullable: true),
                    RoleId = table.Column<string>(nullable: true),
                    TournamentId = table.Column<string>(nullable: true),
                    Permissions = table.Column<string>(nullable: true),
                    Old = table.Column<bool>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.ID);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 1. Drop the Roles table
            migrationBuilder.DropTable(name: "Roles");

            // 2. Rename current AuthorizedUsers table
            migrationBuilder.Sql("ALTER TABLE AuthorizedUsers RENAME TO _AuthorizedUsersTemp;");

            // 3. Recreate original AuthorizedUsers table with PermissionFlags and without Roles
            migrationBuilder.Sql(@"
            CREATE TABLE AuthorizedUsers (
                ID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                Guid TEXT NOT NULL,
                TournamentId TEXT NOT NULL,
                DiscordId TEXT NOT NULL,
                PermissionFlags INTEGER NOT NULL DEFAULT 0,
                Old INTEGER NOT NULL
            );
        ");

            // 4. Copy back data, filling in PermissionFlags with 0
            migrationBuilder.Sql(@"
            INSERT INTO AuthorizedUsers (ID, Guid, TournamentId, DiscordId, PermissionFlags, Old)
            SELECT ID, Guid, TournamentId, DiscordId, 0, Old FROM _AuthorizedUsersTemp;
        ");

            // 5. Drop the temp table
            migrationBuilder.Sql("DROP TABLE _AuthorizedUsersTemp;");
        }
    }

}
