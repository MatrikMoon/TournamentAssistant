using Microsoft.EntityFrameworkCore.Migrations;

namespace TournamentAssistantServer.Migrations
{
    public partial class InitialTournamentMigration : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Pools",
                columns: table => new
                {
                    ID = table.Column<ulong>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Guid = table.Column<string>(nullable: true),
                    TournamentId = table.Column<string>(nullable: true),
                    Name = table.Column<string>(nullable: true),
                    Old = table.Column<bool>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pools", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "PoolSongs",
                columns: table => new
                {
                    ID = table.Column<ulong>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Guid = table.Column<string>(nullable: true),
                    PoolId = table.Column<string>(nullable: true),
                    Name = table.Column<string>(nullable: true),
                    LevelId = table.Column<string>(nullable: true),
                    Characteristic = table.Column<string>(nullable: true),
                    BeatmapDifficulty = table.Column<int>(nullable: false),
                    GameOptions = table.Column<int>(nullable: false),
                    PlayerOptions = table.Column<int>(nullable: false),
                    ShowScoreboard = table.Column<bool>(nullable: false),
                    Attempts = table.Column<int>(nullable: false),
                    DisablePause = table.Column<bool>(nullable: false),
                    DisableFail = table.Column<bool>(nullable: false),
                    DisableScoresaberSubmission = table.Column<bool>(nullable: false),
                    DisableCustomNotesOnStream = table.Column<bool>(nullable: false),
                    Old = table.Column<bool>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PoolSongs", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "Teams",
                columns: table => new
                {
                    ID = table.Column<ulong>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Guid = table.Column<string>(nullable: true),
                    TournamentId = table.Column<string>(nullable: true),
                    Name = table.Column<string>(nullable: true),
                    Image = table.Column<string>(nullable: true),
                    Old = table.Column<bool>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Teams", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "Tournaments",
                columns: table => new
                {
                    ID = table.Column<ulong>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Guid = table.Column<string>(nullable: true),
                    HashedPassword = table.Column<string>(nullable: true),
                    Name = table.Column<string>(nullable: true),
                    Image = table.Column<string>(nullable: true),
                    EnableTeams = table.Column<bool>(nullable: false),
                    ScoreUpdateFrequency = table.Column<int>(nullable: false),
                    BannedMods = table.Column<string>(nullable: true),
                    ServerAddress = table.Column<string>(nullable: true),
                    ServerName = table.Column<string>(nullable: true),
                    ServerPort = table.Column<string>(nullable: true),
                    ServerWebsocketPort = table.Column<string>(nullable: true),
                    Old = table.Column<bool>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tournaments", x => x.ID);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Pools");

            migrationBuilder.DropTable(
                name: "PoolSongs");

            migrationBuilder.DropTable(
                name: "Teams");

            migrationBuilder.DropTable(
                name: "Tournaments");
        }
    }
}
