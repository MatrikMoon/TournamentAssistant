using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace TournamentAssistantShared.Tarf
{
    // Builds the map zip embedded in a replay. Only the files needed to play the recorded difficulty are kept:
    // Info.dat, the difficulty file and its lightshow, the BPM data, the cover image and the song
    // Chroma and Noodle Extensions keep their data inside the difficulty file, so nothing else is needed for them
    public static class MapArchiveBuilder
    {
        // Returns null when the folder has no Info.dat or the difficulty isn't in it
        public static byte[] FromFolder(string folder, string characteristic, int difficulty)
        {
            if (!Directory.Exists(folder)) return null;

            var files = Directory.GetFiles(folder).ToDictionary(Path.GetFileName, x => x, StringComparer.OrdinalIgnoreCase);
            return Build(files.Keys, name => File.ReadAllBytes(files[name]), characteristic, difficulty);
        }

        // Trims a whole map zip, such as one downloaded from BeatSaver, down to one difficulty
        public static byte[] FromArchive(byte[] archive, string characteristic, int difficulty)
        {
            using var input = new ZipArchive(new MemoryStream(archive), ZipArchiveMode.Read);

            // Maps are sometimes zipped inside a folder, so files are looked up by their name only
            var entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in input.Entries.Where(x => x.Name != ""))
            {
                entries[entry.Name] = entry;
            }

            return Build(entries.Keys, name =>
            {
                using var stream = entries[name].Open();
                using var copy = new MemoryStream();
                stream.CopyTo(copy);
                return copy.ToArray();
            }, characteristic, difficulty);
        }

        private static byte[] Build(IEnumerable<string> fileNames, Func<string, byte[]> readFile, string characteristic, int difficulty)
        {
            var names = new HashSet<string>(fileNames, StringComparer.OrdinalIgnoreCase);
            var infoName = names.FirstOrDefault(x => x.Equals("Info.dat", StringComparison.OrdinalIgnoreCase));
            if (infoName == null) return null;

            var info = readFile(infoName);
            var wanted = WantedFiles(JObject.Parse(System.Text.Encoding.UTF8.GetString(info)), characteristic, difficulty);
            if (wanted == null) return null;

            using var output = new MemoryStream();
            using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
            {
                AddEntry(zip, infoName, info);
                foreach (var name in wanted.Where(x => !string.IsNullOrEmpty(x) && names.Contains(x)).Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    AddEntry(zip, names.First(x => x.Equals(name, StringComparison.OrdinalIgnoreCase)), readFile(name));
                }
            }
            return output.ToArray();
        }

        // Returns null when the difficulty isn't listed in the Info.dat
        private static List<string> WantedFiles(JObject info, string characteristic, int difficulty)
        {
            var difficultyName = difficulty >= 0 && difficulty < TarfValues.DifficultyNames.Length ? TarfValues.DifficultyNames[difficulty] : "";
            bool Matches(JToken value, string expected) => string.Equals((string)value, expected, StringComparison.OrdinalIgnoreCase);

            // Version 4 Info.dat
            if (info["audio"] is JObject audio)
            {
                var beatmap = (info["difficultyBeatmaps"] as JArray)?.FirstOrDefault(x =>
                    Matches(x["characteristic"], characteristic) && Matches(x["difficulty"], difficultyName));
                if (beatmap == null) return null;

                return new List<string>
                {
                    (string)beatmap["beatmapDataFilename"],
                    (string)beatmap["lightshowDataFilename"],
                    (string)audio["songFilename"],
                    (string)audio["audioDataFilename"],
                    (string)info["coverImageFilename"],
                };
            }

            // Version 2 and 3 Info.dat
            var set = (info["_difficultyBeatmapSets"] as JArray)?.FirstOrDefault(x => Matches(x["_beatmapCharacteristicName"], characteristic));
            var difficultyBeatmap = (set?["_difficultyBeatmaps"] as JArray)?.FirstOrDefault(x => Matches(x["_difficulty"], difficultyName));
            if (difficultyBeatmap == null) return null;

            return new List<string>
            {
                (string)difficultyBeatmap["_beatmapFilename"],
                (string)info["_songFilename"],
                (string)info["_coverImageFilename"],
                // Not listed in the Info.dat, but read by mods and viewers when it is there
                "BPMInfo.dat",
            };
        }

        // Even ogg audio shrinks a little with deflate, and deflate never makes data meaningfully bigger, so every file is compressed
        private static void AddEntry(ZipArchive zip, string name, byte[] data)
        {
            using var stream = zip.CreateEntry(name, CompressionLevel.Optimal).Open();
            stream.Write(data, 0, data.Length);
        }
    }
}
