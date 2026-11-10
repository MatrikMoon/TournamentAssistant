using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TournamentAssistantShared.Models.Tarf;

namespace TournamentAssistantShared.Tarf
{
    // The map files embedded in a replay. Only the files needed to play the recorded difficulty are kept:
    // Info.dat, the difficulty file and its lightshow, the BPM data, the cover image and the song
    // Chroma and Noodle Extensions keep their data inside the difficulty file, so nothing else is needed for them
    public static class MapFiles
    {
        // Returns null when the folder has no Info.dat or the difficulty isn't in it
        public static List<MapFile> FromFolder(string folder, string characteristic, int difficulty)
        {
            if (!Directory.Exists(folder)) return null;

            var files = Directory.GetFiles(folder).ToDictionary(Path.GetFileName, x => x, StringComparer.OrdinalIgnoreCase);
            return Build(files.Keys, name => File.ReadAllBytes(files[name]), characteristic, difficulty);
        }

        // Trims a whole map zip, such as one downloaded from BeatSaver, down to one difficulty
        public static List<MapFile> FromArchive(byte[] archive, string characteristic, int difficulty)
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

        private static List<MapFile> Build(IEnumerable<string> fileNames, Func<string, byte[]> readFile, string characteristic, int difficulty)
        {
            var names = new HashSet<string>(fileNames, StringComparer.OrdinalIgnoreCase);
            var infoName = names.FirstOrDefault(x => x.Equals("Info.dat", StringComparison.OrdinalIgnoreCase));
            if (infoName == null) return null;

            var info = readFile(infoName);
            if (!(PackedJson.Parse(info) is JObject infoObject)) return null;
            var wanted = WantedFiles(infoObject, characteristic, difficulty);
            if (wanted == null) return null;

            var files = new List<MapFile> { Encode(infoName, info) };
            foreach (var name in wanted.Where(x => !string.IsNullOrEmpty(x) && names.Contains(x)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                files.Add(Encode(names.First(x => x.Equals(name, StringComparison.OrdinalIgnoreCase)), readFile(name)));
            }
            return files;
        }

        // Uses whichever encoding is smallest. Map JSON is packed into columns when that beats deflating its text
        public static MapFile Encode(string name, byte[] data)
        {
            var file = new MapFile { Name = name, Encoding = MapFileEncoding.MapFileEncodingStored, Data = data };

            var deflated = TarfContainer.Deflate(data);
            if (deflated.Length < file.Data.Length)
            {
                file.Encoding = MapFileEncoding.MapFileEncodingDeflate;
                file.Data = deflated;
            }

            var extension = Path.GetExtension(name);
            if (extension.Equals(".dat", StringComparison.OrdinalIgnoreCase) || extension.Equals(".json", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var packed = TarfContainer.Deflate(PackedJson.Pack(PackedJson.Parse(data)));
                    if (packed.Length < file.Data.Length)
                    {
                        file.Encoding = MapFileEncoding.MapFileEncodingPackedJson;
                        file.Data = packed;
                    }
                }
                catch (JsonException)
                {
                    // Not JSON after all, the file stays as it is
                }
            }
            return file;
        }

        // The file as a map folder has it. Packed JSON comes back as compact JSON text with the same values
        public static byte[] Read(MapFile file)
        {
            switch (file.Encoding)
            {
                case MapFileEncoding.MapFileEncodingStored:
                    return file.Data;
                case MapFileEncoding.MapFileEncodingDeflate:
                    return TarfContainer.Inflate(file.Data);
                case MapFileEncoding.MapFileEncodingPackedJson:
                    return System.Text.Encoding.UTF8.GetBytes(PackedJson.Unpack(TarfContainer.Inflate(file.Data)).ToString(Formatting.None));
                default:
                    throw new InvalidDataException($"{file.Name} has an unknown encoding {file.Encoding}");
            }
        }

        // A zip of the embedded files, to open the map in an editor or another viewer
        public static byte[] ToArchive(IEnumerable<MapFile> files)
        {
            using var output = new MemoryStream();
            using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (var file in files)
                {
                    var data = Read(file);
                    using var stream = zip.CreateEntry(file.Name, CompressionLevel.Optimal).Open();
                    stream.Write(data, 0, data.Length);
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
    }
}
