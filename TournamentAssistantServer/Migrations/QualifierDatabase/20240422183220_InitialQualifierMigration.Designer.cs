﻿// <auto-generated />
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using TournamentAssistantServer.Database.Contexts;

namespace TournamentAssistantServer.Migrations.QualifierDatabase
{
    [DbContext(typeof(QualifierDatabaseContext))]
    [Migration("20240422183220_InitialQualifierMigration")]
    partial class InitialQualifierMigration
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "3.1.29");

            modelBuilder.Entity("TournamentAssistantServer.Database.Models.Qualifier", b =>
                {
                    b.Property<ulong>("ID")
                        .ValueGeneratedOnAdd()
                        .HasColumnName("ID")
                        .HasColumnType("INTEGER");

                    b.Property<int>("Flags")
                        .HasColumnName("Flags")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Guid")
                        .HasColumnName("Guid")
                        .HasColumnType("TEXT");

                    b.Property<string>("Image")
                        .HasColumnName("Image")
                        .HasColumnType("TEXT");

                    b.Property<string>("InfoChannelId")
                        .HasColumnName("InfoChannelId")
                        .HasColumnType("TEXT");

                    b.Property<string>("InfoChannelName")
                        .HasColumnName("InfoChannelName")
                        .HasColumnType("TEXT");

                    b.Property<string>("Name")
                        .HasColumnName("Name")
                        .HasColumnType("TEXT");

                    b.Property<bool>("Old")
                        .HasColumnName("Old")
                        .HasColumnType("INTEGER");

                    b.Property<int>("Sort")
                        .HasColumnName("Sort")
                        .HasColumnType("INTEGER");

                    b.Property<string>("TournamentId")
                        .HasColumnName("TournamentId")
                        .HasColumnType("TEXT");

                    b.HasKey("ID");

                    b.ToTable("Qualifiers");
                });

            modelBuilder.Entity("TournamentAssistantServer.Database.Models.QualifierSong", b =>
                {
                    b.Property<ulong>("ID")
                        .ValueGeneratedOnAdd()
                        .HasColumnName("ID")
                        .HasColumnType("INTEGER");

                    b.Property<int>("Attempts")
                        .HasColumnName("Attempts")
                        .HasColumnType("INTEGER");

                    b.Property<int>("BeatmapDifficulty")
                        .HasColumnName("BeatmapDifficulty")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Characteristic")
                        .HasColumnName("Characteristic")
                        .HasColumnType("TEXT");

                    b.Property<bool>("DisableCustomNotesOnStream")
                        .HasColumnName("DisableCustomNotesOnStream")
                        .HasColumnType("INTEGER");

                    b.Property<bool>("DisableFail")
                        .HasColumnName("DisableFail")
                        .HasColumnType("INTEGER");

                    b.Property<bool>("DisablePause")
                        .HasColumnName("DisablePause")
                        .HasColumnType("INTEGER");

                    b.Property<bool>("DisableScoresaberSubmission")
                        .HasColumnName("DisableScoresaberSubmission")
                        .HasColumnType("INTEGER");

                    b.Property<string>("EventId")
                        .HasColumnName("EventId")
                        .HasColumnType("TEXT");

                    b.Property<int>("GameOptions")
                        .HasColumnName("GameOptions")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Guid")
                        .HasColumnName("Guid")
                        .HasColumnType("TEXT");

                    b.Property<string>("LeaderboardMessageId")
                        .HasColumnName("LeaderboardMessageId")
                        .HasColumnType("TEXT");

                    b.Property<string>("LevelId")
                        .HasColumnName("LevelId")
                        .HasColumnType("TEXT");

                    b.Property<string>("Name")
                        .HasColumnName("Name")
                        .HasColumnType("TEXT");

                    b.Property<bool>("Old")
                        .HasColumnName("Old")
                        .HasColumnType("INTEGER");

                    b.Property<int>("PlayerOptions")
                        .HasColumnName("PlayerOptions")
                        .HasColumnType("INTEGER");

                    b.Property<bool>("ShowScoreboard")
                        .HasColumnName("ShowScoreboard")
                        .HasColumnType("INTEGER");

                    b.HasKey("ID");

                    b.ToTable("QualifierSongs");
                });

            modelBuilder.Entity("TournamentAssistantServer.Database.Models.Score", b =>
                {
                    b.Property<ulong>("ID")
                        .ValueGeneratedOnAdd()
                        .HasColumnName("ID")
                        .HasColumnType("INTEGER");

                    b.Property<double>("Accuracy")
                        .HasColumnName("Accuracy")
                        .HasColumnType("REAL");

                    b.Property<int>("BadCuts")
                        .HasColumnName("BadCuts")
                        .HasColumnType("INTEGER");

                    b.Property<int>("BeatmapDifficulty")
                        .HasColumnName("BeatmapDifficulty")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Characteristic")
                        .HasColumnName("Characteristic")
                        .HasColumnType("TEXT");

                    b.Property<string>("EventId")
                        .HasColumnName("EventId")
                        .HasColumnType("TEXT");

                    b.Property<bool>("FullCombo")
                        .HasColumnName("FullCombo")
                        .HasColumnType("INTEGER");

                    b.Property<int>("GameOptions")
                        .HasColumnName("GameOptions")
                        .HasColumnType("INTEGER");

                    b.Property<int>("GoodCuts")
                        .HasColumnName("GoodCuts")
                        .HasColumnType("INTEGER");

                    b.Property<bool>("IsPlaceholder")
                        .HasColumnName("IsPlaceholder")
                        .HasColumnType("INTEGER");

                    b.Property<string>("LevelId")
                        .HasColumnName("LevelId")
                        .HasColumnType("TEXT");

                    b.Property<string>("MapId")
                        .HasColumnName("MapId")
                        .HasColumnType("TEXT");

                    b.Property<int>("MaxCombo")
                        .HasColumnName("MaxCombo")
                        .HasColumnType("INTEGER");

                    b.Property<int>("MaxPossibleScore")
                        .HasColumnName("MaxPossibleScore")
                        .HasColumnType("INTEGER");

                    b.Property<int>("ModifiedScore")
                        .HasColumnName("ModifiedScore")
                        .HasColumnType("INTEGER");

                    b.Property<int>("MultipliedScore")
                        .HasColumnName("MultipliedScore")
                        .HasColumnType("INTEGER");

                    b.Property<int>("NotesMissed")
                        .HasColumnName("NotesMissed")
                        .HasColumnType("INTEGER");

                    b.Property<bool>("Old")
                        .HasColumnName("Old")
                        .HasColumnType("INTEGER");

                    b.Property<string>("PlatformId")
                        .HasColumnName("PlatformId")
                        .HasColumnType("TEXT");

                    b.Property<int>("PlayerOptions")
                        .HasColumnName("PlayerOptions")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Username")
                        .HasColumnName("Username")
                        .HasColumnType("TEXT");

                    b.HasKey("ID");

                    b.ToTable("Scores");
                });
#pragma warning restore 612, 618
        }
    }
}
