using System.Text.Json;
using TARF.Bsor;
using TournamentAssistantShared.Tarf;
using TournamentAssistantShared.Models.Tarf;
using TARF.ScoreSaber;

public static class Program
{
    private static readonly string WriterName = $"TARF {typeof(Program).Assembly.GetName().Version.ToString(3)}";

    private const string Usage = @"Usage:
  TARF encode <replay.bsor | replay.dat> [options]
      Converts a BeatLeader (.bsor) or ScoreSaber (.dat) replay into a .tarf file.
      -output <path>            Where to write the .tarf file. Defaults to the input path with a .tarf extension.
      -hsv <path>               HitScoreVisualizer config (.json) to embed.
      -map <path>               Zip of the map to embed. Only the recorded difficulty is kept.
      -downloadMap true         Download the map from BeatSaver and embed the recorded difficulty.
      -positionStepNm <n>       Position precision in nanometers. Defaults to 10000 (0.01 mm).
      -rotationStepNano <n>     Quaternion precision in billionths. Defaults to 1000.
      -anchorInterval <n>       Frames per motion block, including its anchor frame. Defaults to 60.

  TARF decode <replay.tarf> [options]
      Converts a .tarf file back into a BeatLeader (.bsor) replay.
      -output <path>            Where to write the .bsor file. Defaults to the input path with a .bsor extension.
      -hsv <path>               Also save the embedded HitScoreVisualizer config to this path.
      -map <path>               Also save the embedded map zip to this path.

  TARF edit <replay.tarf> [options]
      Adds or removes the HSV config and the map. The recording itself is copied as it is.
      -output <path>            Where to write the edited .tarf file. Defaults to replacing the input.
      -hsv <path>               HitScoreVisualizer config (.json) to embed, replacing the current one.
      -stripHsv true            Remove the embedded HitScoreVisualizer config.
      -map <path>               Zip of the map to embed, replacing the current one. Only the recorded difficulty is kept.
      -downloadMap true         Download the map from BeatSaver and embed the recorded difficulty.
      -stripMap true            Remove the embedded map.

  TARF info <replay.tarf>
      Prints what a .tarf file contains.";

    public static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine(Usage);
            return 1;
        }

        try
        {
            var command = args[0];
            var input = args[1];
            var optionArgs = args.Skip(2).ToArray();

            switch (command)
            {
                case "encode":
                    Encode(input, ParseOptions(optionArgs, "output", "hsv", "map", "downloadMap", "positionStepNm", "rotationStepNano", "anchorInterval"));
                    return 0;
                case "decode":
                    Decode(input, ParseOptions(optionArgs, "output", "hsv", "map"));
                    return 0;
                case "edit":
                    Edit(input, ParseOptions(optionArgs, "output", "hsv", "stripHsv", "map", "downloadMap", "stripMap"));
                    return 0;
                case "info":
                    ParseOptions(optionArgs);
                    PrintInfo(input);
                    return 0;
                default:
                    Console.WriteLine($"Unknown command \"{command}\"");
                    Console.WriteLine(Usage);
                    return 1;
            }
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"Error: {e.Message}");
            return 1;
        }
    }

    private static void Encode(string input, Dictionary<string, string> options)
    {
        var data = File.ReadAllBytes(input);
        var motionEncoding = new MotionEncoding
        {
            PositionStepNm = GetUInt(options, "positionStepNm", TarfRecorder.DefaultPositionStepNm),
            RotationStepNano = GetUInt(options, "rotationStepNano", TarfRecorder.DefaultRotationStepNano),
            AnchorInterval = GetUInt(options, "anchorInterval", TarfRecorder.DefaultAnchorInterval)
        };

        TarfReplay replay;
        if (BsorReader.IsBsor(data))
        {
            replay = BsorConverter.ToTarf(BsorReader.Read(data), motionEncoding);
        }
        else if (ScoreSaberReader.IsScoreSaberReplay(data))
        {
            replay = ScoreSaberConverter.ToTarf(ScoreSaberReader.Read(data), motionEncoding);
        }
        else
        {
            throw new InvalidDataException($"{input} is neither a BSOR nor a ScoreSaber replay");
        }

        replay.Header.Recorder.Writer = WriterName;

        if (options.TryGetValue("hsv", out var hsvPath))
        {
            replay.Header.Hsv = LoadHsvProfile(hsvPath, replay.Header.Player.Platform);
        }

        var map = replay.Header.Map;
        EmbedMap(map, options);

        var output = options.TryGetValue("output", out var outputPath) ? outputPath : Path.ChangeExtension(input, ".tarf");
        var tarf = TarfWriter.Write(replay);
        File.WriteAllBytes(output, tarf);

        Console.WriteLine($"Wrote {output}");
        var replaySize = tarf.Length - (map.Archive?.Length ?? 0);
        Console.WriteLine($"  {data.Length:N0} bytes -> {tarf.Length:N0} bytes, the replay without its map is {(double)data.Length / replaySize:0.00}x smaller");
    }

    private static void Edit(string input, Dictionary<string, string> options)
    {
        if (options.ContainsKey("hsv") && IsTrue(options, "stripHsv")) throw new ArgumentException("-hsv and -stripHsv can't be used together");
        if ((options.ContainsKey("map") || IsTrue(options, "downloadMap")) && IsTrue(options, "stripMap")) throw new ArgumentException("A map can't be embedded and stripped at once");

        var data = File.ReadAllBytes(input);
        var header = TarfReader.ReadHeader(data);

        if (options.TryGetValue("hsv", out var hsvPath))
        {
            header.Hsv = LoadHsvProfile(hsvPath, header.Player?.Platform);
        }
        else if (IsTrue(options, "stripHsv"))
        {
            header.Hsv = null;
        }

        header.Map ??= new Map();
        if (IsTrue(options, "stripMap"))
        {
            header.Map.Archive = null;
        }
        else
        {
            EmbedMap(header.Map, options);
        }

        var output = options.TryGetValue("output", out var outputPath) ? outputPath : input;
        var tarf = TarfWriter.ReplaceHeader(data, header);
        File.WriteAllBytes(output, tarf);

        Console.WriteLine($"Wrote {output}");
        Console.WriteLine($"  {data.Length:N0} bytes -> {tarf.Length:N0} bytes");
    }

    private static void EmbedMap(Map map, Dictionary<string, string> options)
    {
        if (options.TryGetValue("map", out var mapPath))
        {
            map.Archive = TrimMapArchive(File.ReadAllBytes(mapPath), map);
        }
        else if (IsTrue(options, "downloadMap"))
        {
            map.Archive = TrimMapArchive(DownloadMap(map.Hash), map);
        }
    }

    private static bool IsTrue(Dictionary<string, string> options, string name) =>
        options.TryGetValue(name, out var value) && bool.TryParse(value, out var enabled) && enabled;

    private static void Decode(string input, Dictionary<string, string> options)
    {
        var replay = TarfReader.Read(File.ReadAllBytes(input));

        var output = options.TryGetValue("output", out var outputPath) ? outputPath : Path.ChangeExtension(input, ".bsor");
        File.WriteAllBytes(output, BsorWriter.Write(BsorConverter.FromTarf(replay)));
        Console.WriteLine($"Wrote {output}");

        if (options.TryGetValue("hsv", out var hsvPath))
        {
            if (replay.Header.Hsv == null) Console.WriteLine("  The replay has no HSV config to save");
            else
            {
                File.WriteAllText(hsvPath, replay.Header.Hsv.Json);
                Console.WriteLine($"Wrote {hsvPath}");
            }
        }

        if (options.TryGetValue("map", out var mapPath))
        {
            var archive = replay.Header.Map?.Archive;
            if (archive == null || archive.Length == 0) Console.WriteLine("  The replay has no map zip to save");
            else
            {
                File.WriteAllBytes(mapPath, archive);
                Console.WriteLine($"Wrote {mapPath}");
            }
        }
    }

    private static void PrintInfo(string input)
    {
        var data = File.ReadAllBytes(input);
        var replay = TarfReader.Read(data);
        var header = replay.Header;

        Console.WriteLine($"File:        {input} ({data.Length:N0} bytes)");
        Console.WriteLine($"Source:      {header.Recorder?.Source} {header.Recorder?.SourceVersion}, written by {header.Recorder?.Writer}");
        Console.WriteLine($"Player:      {header.Player?.Name} ({header.Player?.Id}) on {header.Player?.Platform}, game {header.Player?.GameVersion}");
        Console.WriteLine($"Map:         {header.Map?.SongName} [{header.Map?.Characteristic} {TarfValues.DifficultyName(header.Map?.Difficulty ?? Difficulty.DifficultyUnspecified)}] {header.Map?.Hash}");
        Console.WriteLine($"Environment: {header.Map?.Environment}");
        Console.WriteLine($"Modifiers:   {string.Join(", ", header.Settings?.Modifiers ?? new List<string>())}");
        Console.WriteLine($"Colors:      {(header.Colors != null ? "embedded" : "none")}");
        Console.WriteLine($"HSV:         {(header.Hsv != null ? $"embedded ({header.Hsv.Platform}, {header.Hsv.Json.Length:N0} characters)" : "none")}");
        Console.WriteLine($"Map zip:     {(header.Map?.Archive?.Length > 0 ? $"embedded ({header.Map.Archive.Length:N0} bytes)" : "none")}");
        Console.WriteLine($"Motion:      {replay.Frames.Count:N0} frames, {header.MotionEncoding.PositionStepNm} nm / {header.MotionEncoding.RotationStepNano} nano steps, anchor every {header.MotionEncoding.AnchorInterval} frames");
        Console.WriteLine($"Events:      {replay.Events.Count:N0}");
        foreach (var group in replay.Events.GroupBy(EventKind).OrderBy(x => x.Key))
        {
            Console.WriteLine($"  {group.Key,-18} {group.Count():N0}");
        }

        if (replay.Footer == null) Console.WriteLine("Result:      none, the recording was cut off");
        else Console.WriteLine($"Result:      {replay.Footer.Completion}, score {replay.Footer.FinalScore:N0}");
    }

    private static string EventKind(Event tarfEvent)
    {
        if (tarfEvent.Note != null) return "notes";
        if (tarfEvent.Wall != null) return "walls";
        if (tarfEvent.Height != null) return "heights";
        if (tarfEvent.Pause != null) return "pauses";
        if (tarfEvent.Score != null) return "scores";
        if (tarfEvent.Combo != null) return "combos";
        if (tarfEvent.Multiplier != null) return "multipliers";
        if (tarfEvent.Energy != null) return "energies";
        if (tarfEvent.MidPlayModifier != null) return "mid-play modifiers";
        return "unknown";
    }

    // Keeps only the files of the difficulty the replay was recorded on
    private static byte[] TrimMapArchive(byte[] archive, Map map)
    {
        var trimmed = MapArchiveBuilder.FromArchive(archive, map.Characteristic, TarfValues.ToBeatmapDifficulty(map.Difficulty));
        if (trimmed == null)
        {
            throw new InvalidDataException($"The map has no {map.Characteristic} {TarfValues.DifficultyName(map.Difficulty)} difficulty");
        }
        Console.WriteLine($"  Embedding the map ({archive.Length:N0} bytes, {trimmed.Length:N0} after keeping only the recorded difficulty)");
        return trimmed;
    }

    // Finds the map version with the replay's hash, since a newer upload of the same map has a different hash
    private static byte[] DownloadMap(string hash)
    {
        if (string.IsNullOrEmpty(hash)) throw new InvalidDataException("The replay has no map hash to download the map with");

        using var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"TARF/{typeof(Program).Assembly.GetName().Version.ToString(3)}");

        using var map = JsonDocument.Parse(http.GetStringAsync($"https://api.beatsaver.com/maps/hash/{hash}").GetAwaiter().GetResult());
        var version = map.RootElement.GetProperty("versions").EnumerateArray()
            .FirstOrDefault(x => string.Equals(x.GetProperty("hash").GetString(), hash, StringComparison.OrdinalIgnoreCase));
        if (version.ValueKind != JsonValueKind.Object) throw new InvalidDataException($"BeatSaver has no map version with the hash {hash}");

        Console.WriteLine($"  Downloading {map.RootElement.GetProperty("name").GetString()} from BeatSaver");
        return http.GetByteArrayAsync(version.GetProperty("downloadURL").GetString()).GetAwaiter().GetResult();
    }

    // The JSON is kept exactly as written, it is only parsed to make sure it is valid
    private static HsvProfile LoadHsvProfile(string path, string playerPlatform)
    {
        var json = File.ReadAllText(path);
        try
        {
            using var document = JsonDocument.Parse(json);
        }
        catch (JsonException e)
        {
            throw new InvalidDataException($"{path} is not valid JSON: {e.Message}");
        }

        return new HsvProfile { Json = json, Platform = HsvPlatformFor(playerPlatform) };
    }

    // BeatLeader reports Quest as "oculus" and PC Oculus as "oculuspc". ScoreSaber uses "Quest" and "PC"
    private static HsvPlatform HsvPlatformFor(string playerPlatform)
    {
        var platform = playerPlatform?.ToLowerInvariant() ?? "";
        if (platform == "") return HsvPlatform.HsvPlatformUnspecified;
        if (platform == "oculus" || platform == "quest") return HsvPlatform.HsvPlatformQuest;
        return HsvPlatform.HsvPlatformPc;
    }

    // Options look like "-name value", the same as MockClientConsole
    private static Dictionary<string, string> ParseOptions(string[] args, params string[] allowedOptions)
    {
        var options = new Dictionary<string, string>();
        for (var i = 0; i < args.Length; i += 2)
        {
            if (!args[i].StartsWith("-")) throw new ArgumentException($"Expected an option but got \"{args[i]}\"");

            var name = args[i].Substring(1);
            if (!allowedOptions.Contains(name)) throw new ArgumentException($"Unknown option {args[i]}");
            if (i + 1 >= args.Length) throw new ArgumentException($"Option {args[i]} needs a value");

            options[name] = args[i + 1];
        }
        return options;
    }

    private static uint GetUInt(Dictionary<string, string> options, string name, uint defaultValue)
    {
        if (!options.TryGetValue(name, out var text)) return defaultValue;
        if (!uint.TryParse(text, out var value) || value == 0) throw new ArgumentException($"-{name} must be a whole number greater than zero");
        return value;
    }
}
