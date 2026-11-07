using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using IPA.Utilities;
using TournamentAssistant.UnityUtilities;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Tarf;
using UnityEngine;
using Logger = TournamentAssistantShared.Logger;
using Match = TournamentAssistantShared.Models.Match;
using Tarf = TournamentAssistantShared.Models.Tarf;
using TAGameplayModifiers = TournamentAssistantShared.Models.GameplayModifiers;

namespace TournamentAssistant.Behaviors
{
    // Records the song being played as a .tarf replay, saves it to UserData/TournamentAssistant/Replays and submits it to the server
    //
    // Everything except the poses comes from game events, so a frame only costs reading the poses and a few flags
    // Packing happens in blocks of 60 frames and the map is zipped once the song is over, both away from the per frame work
    internal sealed class ReplayRecorder : MonoBehaviour
    {
        public static ReplayRecorder Instance { get; private set; }
        public static PluginClient Client { get; set; }
        public static Tournament Tournament { get; set; }
        public static Match Match { get; set; }
        public static TournamentAssistantShared.Models.GameplayParameters GameplayParameters { get; set; }

        private readonly TarfRecorder _recorder = new TarfRecorder();
        private readonly System.Collections.Generic.Dictionary<NoteData, NoteCutInfo> _badCutInfos = new System.Collections.Generic.Dictionary<NoteData, NoteCutInfo>();
        private Tarf.Header _header;
        private PlayerTransforms _playerTransforms;
        private AudioTimeSyncController _audio;
        private ScoreController _score;
        private ComboController _combo;
        private GameEnergyCounter _energy;
        private BeatmapObjectManager _beatmapObjects;
        private PlayerHeadAndObstacleInteraction _obstacles;
        private PlayerHeightDetector _heightDetector;
        private PauseController _pauseController;
        private Tarf.Event _openWall;
        private Tarf.MidPlayModifiers _modifiers;
        private long _lastFrameTimeUs = long.MinValue;
        private long _pauseStartedAtUnixMs;
        private float _pauseStartedAtRealtime;
        private bool _recording;
        private bool _finished;

        private void Awake()
        {
            Instance = this;
            DontDestroyOnLoad(this);
            StartCoroutine(Initialize());
        }

        private IEnumerator Initialize()
        {
            yield return new WaitUntil(() => Resources.FindObjectsOfTypeAll<PlayerTransforms>().Any());
            yield return new WaitUntil(() => Resources.FindObjectsOfTypeAll<AudioTimeSyncController>().Any());
            yield return new WaitUntil(() => Resources.FindObjectsOfTypeAll<ScoreController>().Any());

            _playerTransforms = Resources.FindObjectsOfTypeAll<PlayerTransforms>().First();
            _audio = Resources.FindObjectsOfTypeAll<AudioTimeSyncController>().First();
            _score = Resources.FindObjectsOfTypeAll<ScoreController>().First();
            _combo = Resources.FindObjectsOfTypeAll<ComboController>().FirstOrDefault();
            _energy = Resources.FindObjectsOfTypeAll<GameEnergyCounter>().FirstOrDefault();
            _heightDetector = Resources.FindObjectsOfTypeAll<PlayerHeightDetector>().FirstOrDefault();
            _pauseController = Resources.FindObjectsOfTypeAll<PauseController>().FirstOrDefault();

            yield return new WaitUntil(() => Member(_score, "_beatmapObjectManager") != null);
            _beatmapObjects = Member(_score, "_beatmapObjectManager") as BeatmapObjectManager;
            _obstacles = Member(_score, "_playerHeadAndObstacleInteraction") as PlayerHeadAndObstacleInteraction;

            try
            {
                _header = CreateHeader();
            }
            catch (Exception e)
            {
                Logger.Error($"Replay recording could not start: {e.Message}");
                yield break;
            }

            if (_beatmapObjects != null) _beatmapObjects.noteWasCutEvent += CollectBadCutInfo;
            if (_obstacles != null) _obstacles.headDidEnterObstacleEvent += HeadEnteredObstacle;
            if (_heightDetector != null) _heightDetector.playerHeightDidChangeEvent += HeightChanged;
            if (_pauseController != null)
            {
                _pauseController.didPauseEvent += Paused;
                _pauseController.didResumeEvent += Resumed;
            }
            if (_combo != null) _combo.comboDidChangeEvent += ComboChanged;
            if (_energy != null) _energy.gameEnergyDidChangeEvent += EnergyChanged;
            _score.scoringForNoteFinishedEvent += ScoringFinished;
            _score.scoreDidChangeEvent += ScoreChanged;
            _score.multiplierDidChangeEvent += MultiplierChanged;

            _recording = true;
        }

        private void Update()
        {
            if (!_recording) return;

            var time = _audio.songTime;
            var timeUs = TarfValues.ToTimeUs(time);

            // The song time stands still while the game is paused, and those frames would only repeat the same moment
            if (time >= 0 && timeUs != _lastFrameTimeUs)
            {
                _lastFrameTimeUs = timeUs;
                _recorder.AddFrame(new TarfFrame
                {
                    TimeUs = timeUs,
                    Fps = Time.unscaledDeltaTime > 0 ? Mathf.RoundToInt(1f / Time.unscaledDeltaTime) : 0,
                    Head = Pose(_playerTransforms.headPseudoLocalPos, _playerTransforms.headPseudoLocalRot),
                    Left = Pose(_playerTransforms.leftHandPseudoLocalPos, _playerTransforms.leftHandPseudoLocalRot),
                    Right = Pose(_playerTransforms.rightHandPseudoLocalPos, _playerTransforms.rightHandPseudoLocalRot),
                });
            }

            if (_openWall != null && _obstacles != null && !_obstacles.playerHeadIsInObstacle) CloseOpenWall();
            RecordMidPlayModifierChanges(timeUs);
        }

        private Tarf.Header CreateHeader()
        {
            var parameters = GameplayParameters ?? Match?.SelectedMap?.GameplayParameters;
            var beatmap = parameters?.Beatmap;
            var self = Client.StateManager.GetUser(Tournament.Guid, Client.StateManager.GetSelfGuid());
            var sceneSetup = Member(Resources.FindObjectsOfTypeAll<GameplayCoreInstaller>().FirstOrDefault(), "_sceneSetupData");
            var playerSettings = Member(sceneSetup, "playerSpecificSettings");
            var levelId = beatmap?.LevelId ?? "";

            var header = new Tarf.Header
            {
                Recorder = new Tarf.Recorder
                {
                    Source = Tarf.ReplaySource.ReplaySourceTournamentAssistant,
                    SourceVersion = TournamentAssistantShared.Constants.PLUGIN_VERSION,
                    Writer = $"TournamentAssistant {TournamentAssistantShared.Constants.PLUGIN_VERSION}",
                },
                RecordedAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Player = new Tarf.Player
                {
                    Id = self?.PlatformId ?? "",
                    Name = self?.Name ?? "",
                    Platform = "PC",
                    GameVersion = Application.version,
                },
                Map = new Tarf.Map
                {
                    Hash = TarfValues.HashFromLevelId(levelId),
                    LevelId = levelId,
                    Characteristic = beatmap?.Characteristic?.SerializedName ?? "Standard",
                    Difficulty = TarfValues.FromBeatmapDifficulty(beatmap?.Difficulty ?? -1),
                    SongName = beatmap?.Name ?? "",
                    Environment = Text(Member(sceneSetup, "targetEnvironmentInfo"), "serializedName"),
                },
                Settings = new Tarf.PlaySettings
                {
                    LeftHanded = Flag(playerSettings, "leftHanded", "_leftHanded"),
                    PlayerHeight = Number(playerSettings, "playerHeight", "_playerHeight"),
                    NoteSpawnOffset = Number(playerSettings, "noteJumpStartBeatOffset", "_noteJumpStartBeatOffset"),
                    SongSpeed = _audio.timeScale,
                    EnvironmentEffectsFilterDefaultPreset = (int)Number(playerSettings, "environmentEffectsFilterDefaultPreset"),
                    EnvironmentEffectsFilterExpertPlusPreset = (int)Number(playerSettings, "environmentEffectsFilterExpertPlusPreset"),
                    NoTextsAndHuds = Flag(playerSettings, "noTextsAndHuds"),
                    SaberTrailIntensity = Number(playerSettings, "saberTrailIntensity"),
                    HideNoteSpawnEffect = Flag(playerSettings, "hideNoteSpawnEffect"),
                    ArcsHapticFeedback = Flag(playerSettings, "arcsHapticFeedback"),
                    ArcVisibility = (int)Number(playerSettings, "arcsVisible", "arcVisibility"),
                },
                Colors = Colors(Member(sceneSetup, "colorScheme")),
                Tournament = new Tarf.TournamentContext
                {
                    TournamentId = Tournament.Guid,
                    MatchId = Match?.Guid ?? "",
                },
                InitialMidPlayModifiers = CurrentMidPlayModifiers(),
            };

            var jumpDistance = Resources.FindObjectsOfTypeAll<UnityEngine.Object>()
                .Where(x => x.GetType().Name.Contains("MovementDataProvider"))
                .Select(x => Number(x, "jumpDistance"))
                .FirstOrDefault(x => x > 0);
            if (jumpDistance > 0) header.Settings.JumpDistance = jumpDistance;

            var modifiers = parameters?.GameplayModifiers?.Options ?? TAGameplayModifiers.GameOptions.None;
            header.Settings.Modifiers.AddRange(Enum.GetValues(typeof(TAGameplayModifiers.GameOptions))
                .Cast<TAGameplayModifiers.GameOptions>()
                .Where(x => x != TAGameplayModifiers.GameOptions.None && modifiers.HasFlag(x))
                .Select(x => x.ToString()));

            header.Mods.AddRange(IPA.Loader.PluginManager.EnabledPlugins.Select(x => new Tarf.Mod
            {
                Name = x.Id ?? x.Name,
                Version = Member(x, "HVersion", "Version")?.ToString() ?? "",
            }));

            var hsvJson = ReplayPlaySettings.ReadSelectedHsvProfile();
            if (!string.IsNullOrEmpty(hsvJson))
            {
                header.Hsv = new Tarf.HsvProfile { Json = hsvJson, Platform = Tarf.HsvPlatform.HsvPlatformPc };
            }

            _modifiers = header.InitialMidPlayModifiers;
            return header;
        }

        private void RecordMidPlayModifierChanges(long timeUs)
        {
            var current = CurrentMidPlayModifiers();
            Tarf.MidPlayModifier toggled;
            if (current.InvertColors != _modifiers.InvertColors) toggled = Tarf.MidPlayModifier.MidPlayModifierInvertColors;
            else if (current.InvertHandedness != _modifiers.InvertHandedness) toggled = Tarf.MidPlayModifier.MidPlayModifierInvertHandedness;
            else if (current.DisableBlueNotes != _modifiers.DisableBlueNotes) toggled = Tarf.MidPlayModifier.MidPlayModifierDisableBlueNotes;
            else if (current.DisableRedNotes != _modifiers.DisableRedNotes) toggled = Tarf.MidPlayModifier.MidPlayModifierDisableRedNotes;
            else return;

            _modifiers = current;
            _recorder.AddEvent(new Tarf.Event
            {
                TimeUs = timeUs,
                MidPlayModifier = new Tarf.MidPlayModifierEvent { Toggled = toggled, State = current },
            });
        }

        private static Tarf.MidPlayModifiers CurrentMidPlayModifiers() => new Tarf.MidPlayModifiers
        {
            InvertColors = MidPlayModifiers.InvertColors,
            InvertHandedness = MidPlayModifiers.InvertHands,
            DisableBlueNotes = MidPlayModifiers.DisableBlueNotes,
            DisableRedNotes = MidPlayModifiers.DisableRedNotes,
        };

        // Bad cuts only report their cut info here, the scoring element that follows doesn't carry it
        private void CollectBadCutInfo(NoteController note, in NoteCutInfo cut)
        {
            if (_recording && note != null) _badCutInfos[note.noteData] = cut;
        }

        private void ScoringFinished(ScoringElement scoring)
        {
            if (!_recording || scoring?.noteData == null) return;

            var data = scoring.noteData;
            var isBomb = data.gameplayType == NoteData.GameplayType.Bomb;
            if (scoring is GoodCutScoringElement goodCut)
            {
                var buffer = goodCut.cutScoreBuffer;
                AddNote(data, Tarf.NoteEventKind.NoteEventKindGoodCut, buffer.noteCutInfo, buffer.beforeCutSwingRating, buffer.afterCutSwingRating);
            }
            else if (scoring is BadCutScoringElement)
            {
                var kind = isBomb ? Tarf.NoteEventKind.NoteEventKindBomb : Tarf.NoteEventKind.NoteEventKindBadCut;
                if (_badCutInfos.TryGetValue(data, out var cut)) AddNote(data, kind, cut, 0, 0);
                else AddNote(data, kind, null, 0, 0);
            }
            else if (scoring is MissScoringElement && !isBomb)
            {
                AddNote(data, Tarf.NoteEventKind.NoteEventKindMiss, null, 0, 0);
            }
            _badCutInfos.Remove(data);
        }

        private void AddNote(NoteData data, Tarf.NoteEventKind kind, NoteCutInfo? cutInfo, float beforeCutRating, float afterCutRating)
        {
            var note = new Tarf.NoteEvent
            {
                Note = new Tarf.NoteIdentity
                {
                    Time = data.time,
                    LineIndex = data.lineIndex,
                    LineLayer = (int)data.noteLineLayer,
                    ColorType = (int)data.colorType,
                    CutDirection = (int)data.cutDirection,
                    ScoringType = (int)data.scoringType,
                    GameplayType = (int)data.gameplayType,
                    CutDirectionAngleOffset = data.cutDirectionAngleOffset,
                },
                Kind = kind,
                UnityTimescale = Time.timeScale,
                TimeSyncTimescale = _audio.timeScale,
            };

            // A cut is timed when the saber went through the note, which is a little before its scoring finished
            var timeUs = TarfValues.ToTimeUs(_audio.songTime);
            if (cutInfo.HasValue)
            {
                var cut = cutInfo.Value;
                note.Cut = new Tarf.NoteCut
                {
                    SpeedOk = cut.speedOK,
                    DirectionOk = cut.directionOK,
                    SaberTypeOk = cut.saberTypeOK,
                    WasCutTooSoon = cut.wasCutTooSoon,
                    SaberSpeed = cut.saberSpeed,
                    SaberDirection = Vector(cut.saberDir),
                    SaberType = (int)cut.saberType,
                    TimeDeviation = cut.timeDeviation,
                    CutDirectionDeviation = cut.cutDirDeviation,
                    CutPoint = Vector(cut.cutPoint),
                    CutNormal = Vector(cut.cutNormal),
                    CutDistanceToCenter = cut.cutDistanceToCenter,
                    CutAngle = cut.cutAngle,
                    BeforeCutRating = beforeCutRating,
                    AfterCutRating = afterCutRating,
                };
                note.WorldRotation = Rotation(cut.worldRotation);
                note.InverseWorldRotation = Rotation(cut.inverseWorldRotation);
                note.NoteRotation = Rotation(cut.noteRotation);
                note.NotePosition = Vector(cut.notePosition);
                timeUs = TarfValues.ToTimeUs(data.time - cut.timeDeviation);
            }

            _recorder.AddEvent(new Tarf.Event { TimeUs = timeUs, Note = note });
        }

        // A wall stays open until the head leaves it, which is when the energy it cost is known
        private void HeadEnteredObstacle(ObstacleController obstacle)
        {
            if (!_recording || obstacle == null || _openWall != null) return;

            var data = obstacle.obstacleData;
            _openWall = new Tarf.Event
            {
                TimeUs = TarfValues.ToTimeUs(_audio.songTime),
                Wall = new Tarf.WallEvent
                {
                    Wall = new Tarf.WallIdentity
                    {
                        Time = data.time,
                        Duration = data.duration,
                        LineIndex = data.lineIndex,
                        LineLayer = (int)data.lineLayer,
                        Width = data.width,
                        Height = data.height,
                    },
                },
            };
        }

        private void CloseOpenWall()
        {
            var exitTimeUs = Math.Max(_openWall.TimeUs, TarfValues.ToTimeUs(_audio.songTime));
            _openWall.Wall.ExitTimeUs = exitTimeUs;
            _openWall.Wall.Energy = _energy != null ? _energy.energy : 0;
            _recorder.AddEvent(_openWall);
            _openWall = null;
        }

        private void HeightChanged(float height) => AddEvent(new Tarf.Event { Height = new Tarf.HeightEvent { Height = height } });

        private void ComboChanged(int combo) => AddEvent(new Tarf.Event { Combo = new Tarf.ComboEvent { Combo = combo } });

        private void EnergyChanged(float energy) => AddEvent(new Tarf.Event { Energy = new Tarf.EnergyEvent { Energy = energy } });

        private void MultiplierChanged(int multiplier, float progress) =>
            AddEvent(new Tarf.Event { Multiplier = new Tarf.MultiplierEvent { Multiplier = multiplier, NextMultiplierProgress = progress } });

        private void ScoreChanged(int multipliedScore, int modifiedScore) =>
            AddEvent(new Tarf.Event { Score = new Tarf.ScoreEvent { Score = multipliedScore, ImmediateMaxPossibleScore = _score.immediateMaxPossibleMultipliedScore } });

        private void Paused()
        {
            _pauseStartedAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _pauseStartedAtRealtime = Time.realtimeSinceStartup;
        }

        private void Resumed()
        {
            AddEvent(new Tarf.Event
            {
                Pause = new Tarf.PauseEvent
                {
                    DurationMs = (long)Math.Round((Time.realtimeSinceStartup - _pauseStartedAtRealtime) * 1000),
                    StartedAtUnixMs = _pauseStartedAtUnixMs,
                    EndedAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                },
            });
        }

        private void AddEvent(Tarf.Event tarfEvent)
        {
            if (!_recording) return;
            tarfEvent.TimeUs = TarfValues.ToTimeUs(_audio.songTime);
            _recorder.AddEvent(tarfEvent);
        }

        public static void Complete(LevelCompletionResults results)
        {
            if (Instance == null) return;

            var footer = new Tarf.Footer { Completion = Tarf.Completion.CompletionIncomplete };
            if (results != null)
            {
                footer.FinalScore = results.multipliedScore;
                if (results.levelEndAction == LevelCompletionResults.LevelEndAction.Quit)
                {
                    footer.Completion = Tarf.Completion.CompletionQuit;
                    footer.EndTimeUs = TarfValues.ToTimeUs(results.endSongTime);
                }
                else if (results.levelEndStateType == LevelCompletionResults.LevelEndStateType.Failed)
                {
                    footer.Completion = Tarf.Completion.CompletionFailed;
                    footer.EndTimeUs = TarfValues.ToTimeUs(results.endSongTime);
                }
                else if (results.levelEndStateType == LevelCompletionResults.LevelEndStateType.Cleared)
                {
                    footer.Completion = Tarf.Completion.CompletionPassed;
                }
            }
            Instance.Finish(footer);
        }

        // Stops recording, then zips the map, writes the file and submits it on a background thread
        private void Finish(Tarf.Footer footer)
        {
            if (!_recording || _finished) return;
            _finished = true;
            if (_openWall != null) CloseOpenWall();
            _recording = false;

            if (_recorder.FrameCount == 0) return;

            var header = _header;
            var recorder = _recorder;
            var client = Client;
            var tournamentId = Tournament.Guid;
            var matchId = Match?.Guid ?? "";
            var levelFolder = FindLevelFolder(header.Map.LevelId);
            var replaysFolder = Path.Combine(UnityGame.UserDataPath ?? "UserData", "TournamentAssistant", "Replays");

            Task.Run(async () =>
            {
                try
                {
                    if (levelFolder != null)
                    {
                        header.Map.Archive = MapArchiveBuilder.FromFolder(levelFolder, header.Map.Characteristic, TarfValues.ToBeatmapDifficulty(header.Map.Difficulty));
                    }

                    var data = recorder.Finish(header, footer);
                    var fileName = TarfValues.FileName(header.Player.Id, header.Map.Hash, header.Map.Characteristic, header.Map.Difficulty, header.RecordedAtUnixMs);
                    Directory.CreateDirectory(replaysFolder);
                    File.WriteAllBytes(Path.Combine(replaysFolder, fileName), data);
                    Logger.Info($"Saved replay {fileName} ({data.Length / 1024} KB)");

                    if (client?.Connected == true)
                    {
                        var response = await client.SubmitReplay(tournamentId, matchId, fileName, data);
                        Logger.Info($"Submitted replay {fileName}: {response?.submit_replay?.Message}");
                    }
                }
                catch (Exception e)
                {
                    Logger.Error($"Failed to save or submit the replay: {e.Message}");
                }
            });
        }

        // Tournament maps are downloaded to CustomLevels/<hash>, anything else is looked up through SongCore
        private static string FindLevelFolder(string levelId)
        {
            try
            {
                var hash = TarfValues.HashFromLevelId(levelId);
                if (hash != "")
                {
                    var downloaded = Path.Combine(CustomLevelPathHelper.customLevelsDirectoryPath, hash);
                    if (Directory.Exists(downloaded)) return downloaded;
                }

                var loader = Type.GetType("SongCore.Loader, SongCore");
                foreach (var name in new[] { "CustomLevels", "CustomWIPLevels" })
                {
                    var levels = loader?.GetField(name, BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as IDictionary
                        ?? loader?.GetProperty(name, BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as IDictionary;
                    if (levels == null) continue;

                    foreach (DictionaryEntry level in levels)
                    {
                        if (string.Equals(Text(level.Value, "levelID"), levelId, StringComparison.OrdinalIgnoreCase)) return level.Key as string;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Debug($"Could not find the map folder for {levelId}: {e.Message}");
            }
            return null;
        }

        private static Tarf.ColorScheme Colors(object colorScheme)
        {
            if (colorScheme == null) return null;

            Tarf.Color ColorValue(string name) => Member(colorScheme, name) is Color color ? TarfValues.ToColor(color.r, color.g, color.b, color.a) : null;
            return new Tarf.ColorScheme
            {
                SaberA = ColorValue("saberAColor"),
                SaberB = ColorValue("saberBColor"),
                Obstacle = ColorValue("obstaclesColor"),
                Environment0 = ColorValue("environmentColor0"),
                Environment1 = ColorValue("environmentColor1"),
                EnvironmentWhite = ColorValue("environmentColorW"),
                Environment0Boost = ColorValue("environmentColor0Boost"),
                Environment1Boost = ColorValue("environmentColor1Boost"),
                EnvironmentWhiteBoost = ColorValue("environmentColorWBoost"),
                SupportsEnvironmentBoost = Flag(colorScheme, "supportsEnvironmentColorBoost"),
            };
        }

        private static TarfPose Pose(Vector3 position, Quaternion rotation) =>
            new TarfPose(new TarfVector3(position.x, position.y, position.z), new TarfQuaternion(rotation.x, rotation.y, rotation.z, rotation.w));

        private static Tarf.Vector3 Vector(Vector3 value) => TarfValues.ToProto(value.x, value.y, value.z);

        private static Tarf.Quaternion Rotation(Quaternion value) => TarfValues.ToProto(value.x, value.y, value.z, value.w);

        // Settings are read by name because their fields moved around between game versions. This only happens once per song
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

        private static float Number(object target, params string[] names)
        {
            try { return Convert.ToSingle(Member(target, names) ?? 0f); }
            catch { return 0; }
        }

        private static bool Flag(object target, params string[] names) => Member(target, names) is bool value && value;

        private static string Text(object target, params string[] names) => Member(target, names) as string ?? "";

        public static void Destroy()
        {
            if (Instance != null) UnityEngine.Object.Destroy(Instance.gameObject);
        }

        private void OnDestroy()
        {
            // Leaving the song without a result, for example through a restart or a disconnect, still keeps what was recorded
            Finish(new Tarf.Footer { Completion = Tarf.Completion.CompletionIncomplete });

            if (_beatmapObjects != null) _beatmapObjects.noteWasCutEvent -= CollectBadCutInfo;
            if (_obstacles != null) _obstacles.headDidEnterObstacleEvent -= HeadEnteredObstacle;
            if (_heightDetector != null) _heightDetector.playerHeightDidChangeEvent -= HeightChanged;
            if (_pauseController != null)
            {
                _pauseController.didPauseEvent -= Paused;
                _pauseController.didResumeEvent -= Resumed;
            }
            if (_combo != null) _combo.comboDidChangeEvent -= ComboChanged;
            if (_energy != null) _energy.gameEnergyDidChangeEvent -= EnergyChanged;
            if (_score != null)
            {
                _score.scoringForNoteFinishedEvent -= ScoringFinished;
                _score.scoreDidChangeEvent -= ScoreChanged;
                _score.multiplierDidChangeEvent -= MultiplierChanged;
            }

            if (Instance == this) Instance = null;
        }
    }
}
