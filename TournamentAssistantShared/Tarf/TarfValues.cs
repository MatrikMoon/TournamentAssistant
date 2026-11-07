using System;
using System.Linq;
using TournamentAssistantShared.Models.Tarf;

namespace TournamentAssistantShared.Tarf
{
    // Conversions shared by every source of replays
    public static class TarfValues
    {
        public static long ToTimeUs(float seconds) => (long)Math.Round(seconds * 1_000_000d);

        public static float ToSeconds(long timeUs) => (float)(timeUs / 1_000_000d);

        public static Vector3 ToProto(TarfVector3 vector) => new Vector3 { X = vector.X, Y = vector.Y, Z = vector.Z };

        public static Vector3 ToProto(float x, float y, float z) => new Vector3 { X = x, Y = y, Z = z };

        public static Quaternion ToProto(TarfQuaternion rotation) => new Quaternion { X = rotation.X, Y = rotation.Y, Z = rotation.Z, W = rotation.W };

        public static Quaternion ToProto(float x, float y, float z, float w) => new Quaternion { X = x, Y = y, Z = z, W = w };

        public static Color ToColor(float r, float g, float b, float a) => new Color { R = r, G = g, B = b, A = a };

        public static TarfVector3 FromProto(Vector3 vector) => vector == null ? default : new TarfVector3(vector.X, vector.Y, vector.Z);

        public static TarfQuaternion FromProto(Quaternion rotation) => rotation == null ? TarfQuaternion.Identity : new TarfQuaternion(rotation.X, rotation.Y, rotation.Z, rotation.W);

        public static readonly string[] DifficultyNames = { "Easy", "Normal", "Hard", "Expert", "ExpertPlus" };

        // Beat Saber numbers difficulties from Easy = 0 to ExpertPlus = 4
        public static Difficulty FromBeatmapDifficulty(int difficulty) =>
            difficulty >= 0 && difficulty < DifficultyNames.Length ? (Difficulty)(difficulty + 1) : Difficulty.DifficultyUnspecified;

        public static int ToBeatmapDifficulty(Difficulty difficulty) => difficulty == Difficulty.DifficultyUnspecified ? -1 : (int)difficulty - 1;

        public static Difficulty DifficultyFromName(string name)
        {
            var index = Array.FindIndex(DifficultyNames, x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase));
            return FromBeatmapDifficulty(index);
        }

        public static string DifficultyName(Difficulty difficulty)
        {
            var index = ToBeatmapDifficulty(difficulty);
            return index >= 0 && index < DifficultyNames.Length ? DifficultyNames[index] : "";
        }

        // Level ids of custom maps end with the map hash, for example "custom_level_DEE2EF2F..."
        public static string HashFromLevelId(string levelId)
        {
            const int hashLength = 40;
            if (string.IsNullOrEmpty(levelId) || levelId.Length < hashLength) return "";

            var hash = levelId.Substring(levelId.Length - hashLength);
            return hash.All(Uri.IsHexDigit) ? hash.ToUpperInvariant() : "";
        }

        // Characters replaced in file name parts. A fixed list, so every platform names the same replay the same way
        private const string FileNameReplacedCharacters = "-/\\:*?\"<>|";

        // userPlatformId-mapHash-mapCharacteristic-mapDifficulty-unixTimestamp.tarf
        public static string FileName(string platformId, string mapHash, string characteristic, Difficulty difficulty, long recordedAtUnixMs)
        {
            var parts = new[] { platformId, mapHash, characteristic, DifficultyName(difficulty), (recordedAtUnixMs / 1000).ToString() };
            return string.Join("-", parts.Select(x => new string((x ?? "").Select(c => c < ' ' || FileNameReplacedCharacters.IndexOf(c) >= 0 ? '_' : c).ToArray()))) + ".tarf";
        }
    }
}
