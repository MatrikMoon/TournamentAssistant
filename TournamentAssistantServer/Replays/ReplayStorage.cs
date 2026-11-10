using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TournamentAssistantShared;
using TournamentAssistantShared.Models;

namespace TournamentAssistantServer.Replays
{
    // Keeps submitted replays in a postgres database
    // When no database is configured or it can't be reached, nothing is stored and lists come back empty. The server keeps running either way
    public class ReplayStorage
    {
        private const string Columns = "guid, tournament_id, match_id, file_name, platform_id, player_name, map_hash, characteristic, difficulty, recorded_at, submitted_at, expires_at, size";

        // Applied in order, each one exactly once. New changes to the table go at the end
        private static readonly string[] Migrations =
        {
            @"CREATE TABLE ta_replays (
                guid uuid PRIMARY KEY,
                tournament_id text NOT NULL,
                match_id text NOT NULL,
                file_name text NOT NULL,
                platform_id text NOT NULL,
                player_name text NOT NULL,
                map_hash text NOT NULL,
                characteristic text NOT NULL,
                difficulty integer NOT NULL,
                recorded_at bigint NOT NULL,
                submitted_at bigint NOT NULL,
                expires_at bigint NOT NULL,
                size bigint NOT NULL,
                data bytea NOT NULL
            );
            CREATE INDEX ta_replays_tournament ON ta_replays (tournament_id, submitted_at DESC);
            CREATE INDEX ta_replays_expires ON ta_replays (expires_at);",
        };

        private readonly string _connectionString;
        private readonly SemaphoreSlim _migrationLock = new SemaphoreSlim(1, 1);
        private bool _migrated;
        private DateTime _lastFailureLog = DateTime.MinValue;

        public bool IsConfigured => _connectionString != null;

        public ReplayStorage(string databaseUrl)
        {
            if (string.IsNullOrWhiteSpace(databaseUrl))
            {
                Logger.Info("No replay database is configured, submitted replays will only be passed on to listeners");
                return;
            }

            try
            {
                _connectionString = ToConnectionString(databaseUrl);
            }
            catch (Exception e)
            {
                Logger.Error($"The replay database URL is invalid, replays won't be stored: {e.Message}");
            }
        }

        public static TimeSpan RetentionPeriod(ReplayRetention retention)
        {
            switch (retention)
            {
                case ReplayRetention.NinetyDays: return TimeSpan.FromDays(90);
                case ReplayRetention.OneYear: return TimeSpan.FromDays(365);
                case ReplayRetention.ThreeYears: return TimeSpan.FromDays(3 * 365);
                default: return TimeSpan.FromDays(30);
            }
        }

        // Fills in the replay's guid, returns false when it couldn't be stored
        public async Task<bool> Store(ReplayRecording replay, byte[] data)
        {
            return await Run(false, async connection =>
            {
                replay.Guid = Guid.NewGuid().ToString();
                using var command = new NpgsqlCommand(
                    $"INSERT INTO ta_replays ({Columns}, data) VALUES (@guid, @tournament_id, @match_id, @file_name, @platform_id, @player_name, @map_hash, @characteristic, @difficulty, @recorded_at, @submitted_at, @expires_at, @size, @data)",
                    connection);
                command.Parameters.AddWithValue("guid", Guid.Parse(replay.Guid));
                command.Parameters.AddWithValue("tournament_id", replay.TournamentId);
                command.Parameters.AddWithValue("match_id", replay.MatchId ?? "");
                command.Parameters.AddWithValue("file_name", replay.FileName);
                command.Parameters.AddWithValue("platform_id", replay.PlatformId ?? "");
                command.Parameters.AddWithValue("player_name", replay.PlayerName ?? "");
                command.Parameters.AddWithValue("map_hash", replay.MapHash ?? "");
                command.Parameters.AddWithValue("characteristic", replay.Characteristic ?? "");
                command.Parameters.AddWithValue("difficulty", replay.Difficulty);
                command.Parameters.AddWithValue("recorded_at", replay.RecordedAt);
                command.Parameters.AddWithValue("submitted_at", replay.SubmittedAt);
                command.Parameters.AddWithValue("expires_at", replay.ExpiresAt);
                command.Parameters.AddWithValue("size", (long)data.Length);
                command.Parameters.AddWithValue("data", data);
                await command.ExecuteNonQueryAsync();
                return true;
            });
        }

        // The newest replays come first. Returns an empty page with available set to false when storage can't be used
        public async Task<(List<ReplayRecording> Replays, int TotalCount, bool Available)> List(string tournamentId, int page, int pageSize)
        {
            var empty = (new List<ReplayRecording>(), 0, false);
            return await Run(empty, async connection =>
            {
                int totalCount;
                using (var count = new NpgsqlCommand("SELECT COUNT(*) FROM ta_replays WHERE tournament_id = @tournament_id", connection))
                {
                    count.Parameters.AddWithValue("tournament_id", tournamentId);
                    totalCount = Convert.ToInt32(await count.ExecuteScalarAsync());
                }

                using var command = new NpgsqlCommand(
                    $"SELECT {Columns} FROM ta_replays WHERE tournament_id = @tournament_id ORDER BY submitted_at DESC LIMIT @limit OFFSET @offset",
                    connection);
                command.Parameters.AddWithValue("tournament_id", tournamentId);
                command.Parameters.AddWithValue("limit", pageSize);
                command.Parameters.AddWithValue("offset", (long)page * pageSize);

                var replays = new List<ReplayRecording>();
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        replays.Add(ReadReplay(reader));
                    }
                }
                return (replays, totalCount, true);
            });
        }

        // Returns null when the replay doesn't exist or storage can't be used
        public async Task<(ReplayRecording Replay, byte[] Data)?> Get(string tournamentId, string replayGuid)
        {
            if (!Guid.TryParse(replayGuid, out var guid)) return null;

            return await Run<(ReplayRecording, byte[])?>(null, async connection =>
            {
                using var command = new NpgsqlCommand(
                    $"SELECT {Columns}, data FROM ta_replays WHERE tournament_id = @tournament_id AND guid = @guid",
                    connection);
                command.Parameters.AddWithValue("tournament_id", tournamentId);
                command.Parameters.AddWithValue("guid", guid);

                using var reader = await command.ExecuteReaderAsync();
                if (!await reader.ReadAsync()) return null;
                return (ReadReplay(reader), (byte[])reader["data"]);
            });
        }

        public async Task<bool> Delete(string tournamentId, string replayGuid)
        {
            if (!Guid.TryParse(replayGuid, out var guid)) return false;

            return await Run(false, async connection =>
            {
                using var command = new NpgsqlCommand("DELETE FROM ta_replays WHERE tournament_id = @tournament_id AND guid = @guid", connection);
                command.Parameters.AddWithValue("tournament_id", tournamentId);
                command.Parameters.AddWithValue("guid", guid);
                return await command.ExecuteNonQueryAsync() > 0;
            });
        }

        public async Task<int> DeleteExpired(long now)
        {
            return await Run(0, async connection =>
            {
                using var command = new NpgsqlCommand("DELETE FROM ta_replays WHERE expires_at <= @now", connection);
                command.Parameters.AddWithValue("now", now);
                return await command.ExecuteNonQueryAsync();
            });
        }

        // Removes the replays of tournaments that no longer exist
        public async Task<int> DeleteOrphans(IEnumerable<string> existingTournamentIds)
        {
            var tournamentIds = existingTournamentIds.ToArray();
            return await Run(0, async connection =>
            {
                using var command = new NpgsqlCommand("DELETE FROM ta_replays WHERE NOT (tournament_id = ANY(@tournament_ids))", connection);
                command.Parameters.AddWithValue("tournament_ids", tournamentIds);
                return await command.ExecuteNonQueryAsync();
            });
        }

        private static ReplayRecording ReadReplay(NpgsqlDataReader reader)
        {
            return new ReplayRecording
            {
                Guid = reader.GetGuid(0).ToString(),
                TournamentId = reader.GetString(1),
                MatchId = reader.GetString(2),
                FileName = reader.GetString(3),
                PlatformId = reader.GetString(4),
                PlayerName = reader.GetString(5),
                MapHash = reader.GetString(6),
                Characteristic = reader.GetString(7),
                Difficulty = reader.GetInt32(8),
                RecordedAt = reader.GetInt64(9),
                SubmittedAt = reader.GetInt64(10),
                ExpiresAt = reader.GetInt64(11),
                Size = reader.GetInt64(12),
            };
        }

        // Every database call goes through here, so a database that is down only ever costs a logged warning and the fallback value
        private async Task<T> Run<T>(T fallback, Func<NpgsqlConnection, Task<T>> action)
        {
            if (!IsConfigured) return fallback;

            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();
                await Migrate(connection);
                return await action(connection);
            }
            catch (Exception e)
            {
                // Don't flood the log while the database is down
                if (DateTime.UtcNow - _lastFailureLog > TimeSpan.FromMinutes(1))
                {
                    _lastFailureLog = DateTime.UtcNow;
                    Logger.Warning($"Replay database is unavailable: {e.Message}");
                }
                return fallback;
            }
        }

        private async Task Migrate(NpgsqlConnection connection)
        {
            if (_migrated) return;

            await _migrationLock.WaitAsync();
            try
            {
                if (_migrated) return;

                using var transaction = connection.BeginTransaction();
                using (var create = new NpgsqlCommand("CREATE TABLE IF NOT EXISTS ta_replay_migrations (version integer PRIMARY KEY)", connection, transaction))
                {
                    await create.ExecuteNonQueryAsync();
                }

                // Stops two servers sharing a database from migrating it at the same time
                using (var lockTable = new NpgsqlCommand("LOCK TABLE ta_replay_migrations IN EXCLUSIVE MODE", connection, transaction))
                {
                    await lockTable.ExecuteNonQueryAsync();
                }

                int appliedVersion;
                using (var version = new NpgsqlCommand("SELECT COALESCE(MAX(version), 0) FROM ta_replay_migrations", connection, transaction))
                {
                    appliedVersion = Convert.ToInt32(await version.ExecuteScalarAsync());
                }

                for (var version = appliedVersion + 1; version <= Migrations.Length; version++)
                {
                    Logger.Info($"Applying replay database migration {version}");
                    using (var migration = new NpgsqlCommand(Migrations[version - 1], connection, transaction))
                    {
                        await migration.ExecuteNonQueryAsync();
                    }
                    using (var record = new NpgsqlCommand("INSERT INTO ta_replay_migrations (version) VALUES (@version)", connection, transaction))
                    {
                        record.Parameters.AddWithValue("version", version);
                        await record.ExecuteNonQueryAsync();
                    }
                }

                await transaction.CommitAsync();
                _migrated = true;
            }
            finally
            {
                _migrationLock.Release();
            }
        }

        // Accepts both postgres://user:password@host:port/database?sslmode=require URLs and regular Npgsql connection strings
        private static string ToConnectionString(string databaseUrl)
        {
            var builder = new NpgsqlConnectionStringBuilder();
            if (databaseUrl.StartsWith("postgres://") || databaseUrl.StartsWith("postgresql://"))
            {
                var uri = new Uri(databaseUrl);
                var userInfo = uri.UserInfo.Split(new[] { ':' }, 2);
                builder.Host = uri.Host;
                builder.Port = uri.Port > 0 ? uri.Port : 5432;
                builder.Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));
                builder.Username = Uri.UnescapeDataString(userInfo[0]);
                if (userInfo.Length > 1) builder.Password = Uri.UnescapeDataString(userInfo[1]);

                foreach (var parameter in uri.Query.TrimStart('?').Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var keyValue = parameter.Split(new[] { '=' }, 2);
                    builder[Uri.UnescapeDataString(keyValue[0])] = keyValue.Length > 1 ? Uri.UnescapeDataString(keyValue[1]) : "";
                }
            }
            else
            {
                builder.ConnectionString = databaseUrl;
            }

            // Fail fast instead of holding up a player's upload when the database is unreachable
            if (!databaseUrl.Contains("Timeout")) builder.Timeout = 5;
            return builder.ConnectionString;
        }
    }
}
