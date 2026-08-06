using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using IPA.Utilities;
using Newtonsoft.Json.Linq;
using TournamentAssistantShared.Models.Replay;
using UnityEngine;
using Logger = TournamentAssistantShared.Logger;

namespace TournamentAssistant.Behaviors
{
    internal static class ReplayPlaySettings
    {
        private const int MaxHsvJsonBytes = 32 * 1024;
        private const int MaxHsvPayloadBytes = 8 * 1024;
        private const int MaxHsvSelectorBytes = 8 * 1024;

        public static ReplayExtension Create(object sceneSetup, Color leftSaber, Color rightSaber,
            float jumpDistance, string environment, int difficulty)
        {
            var settings = Member(sceneSetup, "playerSpecificSettings");
            var colorScheme = Member(sceneSetup, "colorScheme");
            var defaultPreset = Integer(settings, "environmentEffectsFilterDefaultPreset");
            var expertPlusPreset = Integer(settings, "environmentEffectsFilterExpertPlusPreset");
            var currentPreset = difficulty == 4 ? expertPlusPreset : defaultPreset;

            using (var stream = new MemoryStream())
            {
                Float(stream, 1);
                Float(stream, jumpDistance);
                // The gameplay scene setup contains Beat Saber's resolved color scheme,
                // including the player's built-in custom note/saber colors. ColorManager
                // is retained as a fallback for game versions that hide this member.
                ColorValue(stream, NullableColor(colorScheme, "saberAColor") ?? leftSaber);
                ColorValue(stream, NullableColor(colorScheme, "saberBColor") ?? rightSaber);
                ColorValue(stream, NullableColor(colorScheme, "obstaclesColor"));
                ColorValue(stream, NullableColor(colorScheme, "environmentColor0") ?? leftSaber);
                ColorValue(stream, NullableColor(colorScheme, "environmentColor1") ?? rightSaber);
                ColorValue(stream, NullableColor(colorScheme, "environmentColorW"));
                ColorValue(stream, NullableColor(colorScheme, "environmentColor0Boost") ?? leftSaber);
                ColorValue(stream, NullableColor(colorScheme, "environmentColor1Boost") ?? rightSaber);
                ColorValue(stream, NullableColor(colorScheme, "environmentColorWBoost"));
                Boolean(stream, BooleanValue(colorScheme, "supportsEnvironmentColorBoost", true));
                String(stream, environment);
                Integer(stream, defaultPreset);
                Integer(stream, expertPlusPreset);
                Integer(stream, currentPreset);
                Boolean(stream, BooleanValue(settings, "noTextsAndHuds"));
                Float(stream, FloatValue(settings, "saberTrailIntensity"));
                Boolean(stream, BooleanValue(settings, "hideNoteSpawnEffect"));
                Boolean(stream, BooleanValue(settings, "arcsHapticFeedback"));
                Integer(stream, Integer(settings, "arcsVisible", "arcVisibility"));
                return new ReplayExtension
                {
                    Id = "scoresaber.play-settings",
                    Version = 1,
                    Payload = stream.ToArray()
                };
            }
        }

        public static ReplayExtension CreateHsvProfile()
        {
            try
            {
                // ScoreSaber owns the wire codec. If it is not installed/loaded, HSV is
                // deliberately omitted while Beat Saber's built-in custom colors remain.
                var codec = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(x => x.GetType("ScoreSaber.Features.Replays.Format.HsvReplayConfigCodec", false))
                    .FirstOrDefault(x => x != null);
                if (codec == null) return null;

                var userData = UnityGame.UserDataPath ?? "UserData";
                var selector = FindHsvSelector(userData);
                if (selector == null) return null;
                var selectorJson = File.ReadAllText(selector);
                var root = JObject.Parse(selectorJson);
                var selected = root.DescendantsAndSelf()
                    .OfType<JProperty>()
                    .FirstOrDefault(x => string.Equals(x.Name, "ConfigFilePath", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(x.Name, "selectedConfig", StringComparison.OrdinalIgnoreCase))
                    ?.Value?.Value<string>();
                if (string.IsNullOrWhiteSpace(selected)) return null;

                var profileRoot = Path.GetFullPath(Path.Combine(userData, "HitScoreVisualizer"));
                var profilePath = Path.GetFullPath(Path.Combine(profileRoot, selected));
                var rootPrefix = profileRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    + Path.DirectorySeparatorChar;
                if (!profilePath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase) || !File.Exists(profilePath))
                    return null;
                return ProfileExtension(codec, profilePath, File.ReadAllText(profilePath));
            }
            catch (Exception ex)
            {
                Logger.Debug("Failed to record ScoreSaber HSV config: " + ex.Message);
                return null;
            }
        }

        private static string FindHsvSelector(string userData)
        {
            var primary = Path.Combine(userData, "HitScoreVisualizer.json");
            if (File.Exists(primary) && new FileInfo(primary).Length <= MaxHsvSelectorBytes) return primary;
            if (!Directory.Exists(userData)) return null;

            foreach (var path in Directory.EnumerateFiles(userData, "*.json", SearchOption.TopDirectoryOnly))
            {
                var info = new FileInfo(path);
                if (info.Length > MaxHsvSelectorBytes) continue;
                try
                {
                    var root = JObject.Parse(File.ReadAllText(path));
                    if (root.DescendantsAndSelf().OfType<JProperty>().Any(x =>
                        (string.Equals(x.Name, "ConfigFilePath", StringComparison.OrdinalIgnoreCase)
                         || string.Equals(x.Name, "selectedConfig", StringComparison.OrdinalIgnoreCase))
                        && x.Value.Type == JTokenType.String)) return path;
                }
                catch { }
            }
            return null;
        }

        private static ReplayExtension ProfileExtension(Type codec, string path, string json)
        {
            var extension = Path.GetExtension(path);
            if (!string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(extension, ".hsv", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(extension, ".hsvconfig", StringComparison.OrdinalIgnoreCase)) return null;
            if (string.IsNullOrEmpty(json) || Encoding.UTF8.GetByteCount(json) > MaxHsvJsonBytes) return null;

            var encode = codec.GetMethod("TryEncodeJson", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (encode == null) return null;
            var arguments = new object[] { json, null, null };
            if (!(encode.Invoke(null, arguments) is bool success) || !success) return null;
            var payload = arguments[1] as byte[];
            if (payload == null || payload.Length == 0 || payload.Length > MaxHsvPayloadBytes) return null;
            return new ReplayExtension { Id = "scoresaber.hsv-config", Version = 1, Payload = payload };
        }

        private static object Member(object target, params string[] names)
        {
            if (target == null) return null;
            for (var type = target.GetType(); type != null; type = type.BaseType)
                foreach (var name in names)
                {
                    var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (field != null) return field.GetValue(target);
                    var property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (property != null) return property.GetValue(target, null);
                }
            return null;
        }

        private static int Integer(object target, params string[] names)
        {
            try { return Convert.ToInt32(Member(target, names)); }
            catch { return 0; }
        }

        private static float FloatValue(object target, params string[] names)
        {
            try { return Convert.ToSingle(Member(target, names)); }
            catch { return 0; }
        }

        private static bool BooleanValue(object target, string name, bool fallback = false)
        {
            try { return Convert.ToBoolean(Member(target, name)); }
            catch { return fallback; }
        }

        private static Color? NullableColor(object target, params string[] names)
        {
            var value = Member(target, names);
            return value is Color color ? color : (Color?)null;
        }

        private static void Boolean(Stream stream, bool value) => stream.WriteByte(value ? (byte)1 : (byte)0);

        private static void Integer(Stream stream, int value)
        {
            stream.WriteByte((byte)value);
            stream.WriteByte((byte)(value >> 8));
            stream.WriteByte((byte)(value >> 16));
            stream.WriteByte((byte)(value >> 24));
        }

        private static void Float(Stream stream, float value) => Integer(stream, BitConverter.ToInt32(BitConverter.GetBytes(value), 0));

        private static void String(Stream stream, string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            Integer(stream, bytes.Length);
            stream.Write(bytes, 0, bytes.Length);
        }

        private static void ColorValue(Stream stream, Color? value)
        {
            Boolean(stream, value.HasValue);
            if (!value.HasValue) return;
            Float(stream, value.Value.r);
            Float(stream, value.Value.g);
            Float(stream, value.Value.b);
            Float(stream, value.Value.a);
        }
    }
}
