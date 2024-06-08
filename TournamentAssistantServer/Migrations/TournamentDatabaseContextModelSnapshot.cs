﻿// <auto-generated />
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using TournamentAssistantServer.Database.Contexts;

namespace TournamentAssistantServer.Migrations
{
    [DbContext(typeof(TournamentDatabaseContext))]
    partial class TournamentDatabaseContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "3.1.29");

            modelBuilder.Entity("TournamentAssistantServer.Database.Models.Pool", b =>
                {
                    b.Property<ulong>("ID")
                        .ValueGeneratedOnAdd()
                        .HasColumnName("ID")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Guid")
                        .HasColumnName("Guid")
                        .HasColumnType("TEXT");

                    b.Property<string>("Image")
                        .HasColumnName("Image")
                        .HasColumnType("TEXT");

                    b.Property<string>("Name")
                        .HasColumnName("Name")
                        .HasColumnType("TEXT");

                    b.Property<bool>("Old")
                        .HasColumnName("Old")
                        .HasColumnType("INTEGER");

                    b.Property<string>("TournamentId")
                        .HasColumnName("TournamentId")
                        .HasColumnType("TEXT");

                    b.HasKey("ID");

                    b.ToTable("Pools");
                });

            modelBuilder.Entity("TournamentAssistantServer.Database.Models.PoolSong", b =>
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

                    b.Property<int>("GameOptions")
                        .HasColumnName("GameOptions")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Guid")
                        .HasColumnName("Guid")
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

                    b.Property<string>("PoolId")
                        .HasColumnName("PoolId")
                        .HasColumnType("TEXT");

                    b.Property<bool>("ShowScoreboard")
                        .HasColumnName("ShowScoreboard")
                        .HasColumnType("INTEGER");

                    b.HasKey("ID");

                    b.ToTable("PoolSongs");
                });

            modelBuilder.Entity("TournamentAssistantServer.Database.Models.Team", b =>
                {
                    b.Property<ulong>("ID")
                        .ValueGeneratedOnAdd()
                        .HasColumnName("ID")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Guid")
                        .HasColumnName("Guid")
                        .HasColumnType("TEXT");

                    b.Property<string>("Image")
                        .HasColumnName("Image")
                        .HasColumnType("TEXT");

                    b.Property<string>("Name")
                        .HasColumnName("Name")
                        .HasColumnType("TEXT");

                    b.Property<bool>("Old")
                        .HasColumnName("Old")
                        .HasColumnType("INTEGER");

                    b.Property<string>("TournamentId")
                        .HasColumnName("TournamentId")
                        .HasColumnType("TEXT");

                    b.HasKey("ID");

                    b.ToTable("Teams");
                });

            modelBuilder.Entity("TournamentAssistantServer.Database.Models.Tournament", b =>
                {
                    b.Property<ulong>("ID")
                        .ValueGeneratedOnAdd()
                        .HasColumnName("ID")
                        .HasColumnType("INTEGER");

                    b.Property<string>("BannedMods")
                        .HasColumnName("BannedMods")
                        .HasColumnType("TEXT");

                    b.Property<bool>("EnablePools")
                        .HasColumnName("EnablePools")
                        .HasColumnType("INTEGER");

                    b.Property<bool>("EnableTeams")
                        .HasColumnName("EnableTeams")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Guid")
                        .HasColumnName("Guid")
                        .HasColumnType("TEXT");

                    b.Property<string>("HashedPassword")
                        .HasColumnName("HashedPassword")
                        .HasColumnType("TEXT");

                    b.Property<string>("Image")
                        .HasColumnName("Image")
                        .HasColumnType("TEXT");

                    b.Property<string>("Name")
                        .HasColumnName("Name")
                        .HasColumnType("TEXT");

                    b.Property<bool>("Old")
                        .HasColumnName("Old")
                        .HasColumnType("INTEGER");

                    b.Property<int>("ScoreUpdateFrequency")
                        .HasColumnName("ScoreUpdateFrequency")
                        .HasColumnType("INTEGER");

                    b.Property<string>("ServerAddress")
                        .HasColumnName("ServerAddress")
                        .HasColumnType("TEXT");

                    b.Property<string>("ServerName")
                        .HasColumnName("ServerName")
                        .HasColumnType("TEXT");

                    b.Property<string>("ServerPort")
                        .HasColumnName("ServerPort")
                        .HasColumnType("TEXT");

                    b.Property<string>("ServerWebsocketPort")
                        .HasColumnName("ServerWebsocketPort")
                        .HasColumnType("TEXT");

                    b.Property<bool>("ShowQualifierButton")
                        .HasColumnName("ShowQualifierButton")
                        .HasColumnType("INTEGER");

                    b.Property<bool>("ShowTournamentButton")
                        .HasColumnName("ShowTournamentButton")
                        .HasColumnType("INTEGER");

                    b.HasKey("ID");

                    b.ToTable("Tournaments");
                });
#pragma warning restore 612, 618
        }
    }
}
