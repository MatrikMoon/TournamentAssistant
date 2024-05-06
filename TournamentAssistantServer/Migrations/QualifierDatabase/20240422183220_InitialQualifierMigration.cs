using Microsoft.EntityFrameworkCore.Migrations;

namespace TournamentAssistantServer.Migrations.QualifierDatabase
{
    public partial class InitialQualifierMigration : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Qualifiers",
                columns: table => new
                {
                    ID = table.Column<ulong>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Guid = table.Column<string>(nullable: true),
                    Name = table.Column<string>(nullable: true),
                    Image = table.Column<string>(nullable: true),
                    TournamentId = table.Column<string>(nullable: true),
                    InfoChannelId = table.Column<string>(nullable: true),
                    InfoChannelName = table.Column<string>(nullable: true),
                    Flags = table.Column<int>(nullable: false),
                    Sort = table.Column<int>(nullable: false),
                    Old = table.Column<bool>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Qualifiers", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "QualifierSongs",
                columns: table => new
                {
                    ID = table.Column<ulong>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Guid = table.Column<string>(nullable: true),
                    EventId = table.Column<string>(nullable: true),
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
                    LeaderboardMessageId = table.Column<string>(nullable: true),
                    Old = table.Column<bool>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QualifierSongs", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "Scores",
                columns: table => new
                {
                    ID = table.Column<ulong>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EventId = table.Column<string>(nullable: true),
                    MapId = table.Column<string>(nullable: true),
                    LevelId = table.Column<string>(nullable: true),
                    PlatformId = table.Column<string>(nullable: true),
                    Username = table.Column<string>(nullable: true),
                    MultipliedScore = table.Column<int>(nullable: false),
                    ModifiedScore = table.Column<int>(nullable: false),
                    MaxPossibleScore = table.Column<int>(nullable: false),
                    Accuracy = table.Column<double>(nullable: false),
                    NotesMissed = table.Column<int>(nullable: false),
                    BadCuts = table.Column<int>(nullable: false),
                    GoodCuts = table.Column<int>(nullable: false),
                    MaxCombo = table.Column<int>(nullable: false),
                    FullCombo = table.Column<bool>(nullable: false),
                    Characteristic = table.Column<string>(nullable: true),
                    BeatmapDifficulty = table.Column<int>(nullable: false),
                    GameOptions = table.Column<int>(nullable: false),
                    PlayerOptions = table.Column<int>(nullable: false),
                    IsPlaceholder = table.Column<bool>(nullable: false),
                    Old = table.Column<bool>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Scores", x => x.ID);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Qualifiers");

            migrationBuilder.DropTable(
                name: "QualifierSongs");

            migrationBuilder.DropTable(
                name: "Scores");
        }
    }
}
