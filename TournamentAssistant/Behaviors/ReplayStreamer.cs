using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Replay;
using UnityEngine;
using Match = TournamentAssistantShared.Models.Match;

namespace TournamentAssistant.Behaviors
{
    /// <summary>Records replay-shaped gameplay events directly into bounded network chunks.</summary>
    internal sealed class ReplayStreamer : MonoBehaviour
    {
        private const int MaxEventsPerChunk = 64;
        private const float MaxChunkAgeSeconds = 0.25f;

        public static ReplayStreamer Instance { get; private set; }
        public static PluginClient Client { get; set; }
        public static Tournament Tournament { get; set; }
        public static Match Match { get; set; }
        public static TournamentAssistantShared.Models.GameplayParameters GameplayParameters { get; set; }

        private readonly ReplayEventCounts _counts = new ReplayEventCounts();
        private readonly Dictionary<NoteData, NoteCutInfo> _cutInfos = new Dictionary<NoteData, NoteCutInfo>();
        private StreamReplayEventBatch _batch = new StreamReplayEventBatch();
        private PlayerTransforms _playerTransforms;
        private AudioTimeSyncController _audio;
        private ScoreController _score;
        private ComboController _combo;
        private GameEnergyCounter _energy;
        private BeatmapObjectManager _beatmapObjects;
        private string _streamId;
        private ulong _sequence = 1;
        private ulong _chunkCount;
        private int _eventCount;
        private float _batchStartedAt;
        private float _lastTime;
        private int _lastScore = int.MinValue;
        private int _lastCombo = int.MinValue;
        private int _lastMultiplier = int.MinValue;
        private float _lastEnergy = float.NaN;
        private bool _started;
        private bool _ended;

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

            yield return new WaitUntil(() => GetMember(_score, "_beatmapObjectManager") != null);
            _beatmapObjects = GetMember(_score, "_beatmapObjectManager") as BeatmapObjectManager;
            if (_beatmapObjects != null)
            {
                _beatmapObjects.noteWasCutEvent += CollectCutInfo;
            }
            _score.scoringForNoteFinishedEvent += ScoringFinished;

            _streamId = "ta-pc-" + Guid.NewGuid().ToString("N");
            _started = true;
            Send(StartPacket());
            RecordHeight(PlayerHeight());
        }

        private void Update()
        {
            if (!_started || _ended || _audio == null || _playerTransforms == null)
                return;

            var time = _audio.songTime;
            if (time < 0)
                return;
            _lastTime = Math.Max(_lastTime, time);

            _batch.PoseFrames.Add(new ReplayPoseFrame
            {
                Head = Pose(_playerTransforms.headPseudoLocalPos, _playerTransforms.headPseudoLocalRot),
                Left = Pose(_playerTransforms.leftHandPseudoLocalPos, _playerTransforms.leftHandPseudoLocalRot),
                Right = Pose(_playerTransforms.rightHandPseudoLocalPos, _playerTransforms.rightHandPseudoLocalRot),
                Fps = Time.unscaledDeltaTime > 0 ? Mathf.RoundToInt(1f / Time.unscaledDeltaTime) : 90,
                TimeSeconds = time
            });
            _counts.PoseFrames++;
            Mark(time);
            RecordStateChanges(time);

            if (_eventCount >= MaxEventsPerChunk || Time.realtimeSinceStartup - _batchStartedAt >= MaxChunkAgeSeconds)
                Flush();
        }

        private ReplayStreamPacket StartPacket()
        {
            var parameters = GameplayParameters ?? Match?.SelectedMap?.GameplayParameters;
            var beatmap = parameters?.Beatmap;
            var self = Client.StateManager.GetUser(Tournament.Guid, Client.StateManager.GetSelfGuid());
            var colors = CurrentSaberColors();
            var sceneSetup = CurrentSceneSetup();
            var environment = GetString(GetMember(sceneSetup, "targetEnvironmentInfo"), "serializedName");
            var platformId = self?.PlatformId ?? string.Empty;
            var levelId = beatmap?.LevelId ?? string.Empty;
            var rawDifficulty = beatmap?.Difficulty ?? 0;
            var replayDifficulty = ReplayDifficulty(rawDifficulty);
            var packet = new ReplayStreamPacket
            {
                StreamId = _streamId,
                PlayerId = platformId,
                MatchId = Match?.Guid ?? string.Empty,
                Start = new ReplayStreamStart
                {
                    ProtocolVersion = 1,
                    ClientStartTimeUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    GameSessionId = Guid.NewGuid().ToString("N"),
                    Player = new PlayerIdentity
                    {
                        PlayerId = platformId,
                        Platform = ReplayPlatform.ReplayPlatformSteam,
                        GameVersion = Application.version,
                        ClientVersion = TournamentAssistantShared.Constants.PLUGIN_VERSION
                    },
                    Beatmap = new BeatmapIdentity
                    {
                        MapHash = NormalizeHash(levelId),
                        LevelId = levelId,
                        Difficulty = replayDifficulty,
                        DifficultyName = ((BeatmapDifficulty)rawDifficulty).ToString(),
                        Characteristic = beatmap?.Characteristic?.SerializedName ?? "Standard",
                        Environment = environment,
                        Modifiers = { parameters?.GameplayModifiers.Options.ToString() ?? string.Empty }
                    },
                    ReplayMetadata = new StreamReplayMetadata
                    {
                        ReplayVersion = "ta-live-1",
                        LevelId = levelId,
                        Difficulty = replayDifficulty,
                        Characteristic = beatmap?.Characteristic?.SerializedName ?? "Standard",
                        Modifiers = { parameters?.GameplayModifiers.Options.ToString() ?? string.Empty },
                        NoteSpawnOffset = parameters?.PlayerSettings?.NoteJumpStartBeatOffset ?? 0,
                        LeftHanded = parameters?.PlayerSettings?.Options.HasFlag(TournamentAssistantShared.Models.PlayerSpecificSettings.PlayerOptions.LeftHanded) ?? false,
                        InitialHeight = PlayerHeight(),
                        GameVersion = Application.version,
                        PluginVersion = TournamentAssistantShared.Constants.PLUGIN_VERSION,
                        Platform = "PC",
                        SongSpeed = _audio.timeScale,
                        JumpDistance = CurrentJumpDistance(),
                        LeftSaberColor = Color(colors.Item1),
                        RightSaberColor = Color(colors.Item2)
                    }
                }
            };
            packet.Start.ReplayExtensions.Add(ReplayPlaySettings.Create(sceneSetup, colors.Item1, colors.Item2,
                packet.Start.ReplayMetadata.JumpDistance, environment, rawDifficulty));
            var hsvProfile = ReplayPlaySettings.CreateHsvProfile();
            if (hsvProfile != null) packet.Start.ReplayExtensions.Add(hsvProfile);
            return packet;
        }

        // ScoreSaber/Ludus replay metadata uses odd difficulty ratings rather than
        // Beat Saber's zero-based enum: Easy=1, Normal=3, Hard=5, Expert=7, ExpertPlus=9.
        private static int ReplayDifficulty(int difficulty) =>
            difficulty >= 0 && difficulty <= 4 ? difficulty * 2 + 1 : difficulty;

        private void RecordStateChanges(float time)
        {
            if (_score != null && _score.modifiedScore != _lastScore)
            {
                _lastScore = _score.modifiedScore;
                _batch.ScoreEvents.Add(new ReplayScoreEvent
                {
                    Score = _lastScore,
                    ImmediateMaxPossibleScore = _score.immediateMaxPossibleModifiedScore,
                    TimeSeconds = time
                });
                _counts.ScoreEvents++;
                Mark(time);
            }
            var combo = _combo == null ? 0 : GetInt(_combo, "_combo", "combo");
            if (combo != _lastCombo)
            {
                _lastCombo = combo;
                _batch.ComboEvents.Add(new ReplayComboEvent { Combo = combo, TimeSeconds = time });
                _counts.ComboEvents++;
                Mark(time);
            }
            var multiplier = GetInt(GetMember(_score, "_scoreMultiplierCounter"), "multiplier");
            if (multiplier != _lastMultiplier)
            {
                _lastMultiplier = multiplier;
                _batch.MultiplierEvents.Add(new ReplayMultiplierEvent { Multiplier = multiplier, TimeSeconds = time });
                _counts.MultiplierEvents++;
                Mark(time);
            }
            var energy = _energy?.energy ?? 0;
            if (float.IsNaN(_lastEnergy) || Math.Abs(energy - _lastEnergy) > 0.0001f)
            {
                _lastEnergy = energy;
                _batch.EnergyEvents.Add(new ReplayEnergyEvent { Energy = energy, TimeSeconds = time });
                _counts.EnergyEvents++;
                Mark(time);
            }
        }

        private void CollectCutInfo(NoteController note, in NoteCutInfo cut)
        {
            if (_started && note != null)
                _cutInfos[note.noteData] = cut;
        }

        private void ScoringFinished(ScoringElement scoring)
        {
            if (!_started || scoring?.noteData == null) return;
            var data = scoring.noteData;
            if (scoring is GoodCutScoringElement goodCut)
            {
                var buffer = goodCut.cutScoreBuffer;
                _cutInfos.Remove(data);
                AddNote(data, buffer.noteCutInfo, ReplayNoteEventType.ReplayNoteEventTypeGoodCut,
                    buffer.beforeCutSwingRating, buffer.afterCutSwingRating);
                return;
            }
            if (scoring is BadCutScoringElement)
            {
                var type = IsBomb(data) ? ReplayNoteEventType.ReplayNoteEventTypeBomb : ReplayNoteEventType.ReplayNoteEventTypeBadCut;
                if (_cutInfos.TryGetValue(data, out var cut)) AddNote(data, cut, type, 0, 0);
                else AddNote(data, null, type, 0, 0);
                _cutInfos.Remove(data);
                return;
            }
            if (scoring is MissScoringElement && !IsBomb(data))
                AddNote(data, null, ReplayNoteEventType.ReplayNoteEventTypeMiss, 0, 0);
        }

        private void AddNote(NoteData data, NoteCutInfo? cut, ReplayNoteEventType type, float beforeCutRating, float afterCutRating)
        {
            var time = _audio?.songTime ?? GetFloat(data, "time");
            var item = new ReplayNoteEvent
            {
                NoteId = new ReplayNoteId
                {
                    TimeSeconds = GetFloat(data, "time"),
                    LineLayer = GetInt(data, "noteLineLayer", "lineLayer"),
                    LineIndex = GetInt(data, "lineIndex"),
                    ColorType = (int)data.colorType,
                    CutDirection = (int)data.cutDirection,
                    GameplayType = (int)data.gameplayType,
                    ScoringType = (int)data.scoringType
                },
                EventType = type,
                TimeSeconds = time,
                UnityTimescale = Time.timeScale,
                TimeSyncTimescale = _audio?.timeScale ?? 1
            };
            if (cut.HasValue)
            {
                object value = cut.Value;
                item.CutPoint = Vector(GetVector(value, "cutPoint"));
                item.CutNormal = Vector(GetVector(value, "cutNormal"));
                item.SaberDirection = Vector(GetVector(value, "saberDir", "saberDirection"));
                item.SaberType = GetInt(value, "saberType");
                item.DirectionOk = GetBool(value, "directionOK", "directionOk");
                item.SaberSpeed = GetFloat(value, "saberSpeed");
                item.CutAngle = GetFloat(value, "cutAngle");
                item.CutDistanceToCenter = GetFloat(value, "cutDistanceToCenter");
                item.CutDirectionDeviation = GetFloat(value, "cutDirDeviation");
                item.BeforeCutRating = beforeCutRating;
                item.AfterCutRating = afterCutRating;
                var deviation = GetFloat(value, "timeDeviation");
                item.TimeDeviation = deviation;
                item.WorldRotation = Rotation(GetQuaternion(value, "worldRotation"));
                item.InverseWorldRotation = Rotation(GetQuaternion(value, "inverseWorldRotation"));
                item.NoteRotation = Rotation(GetQuaternion(value, "noteRotation"));
                item.NotePosition = Vector(GetVector(value, "notePosition"));
                item.TimeSeconds = GetFloat(data, "time") - deviation;
            }
            _batch.NoteEvents.Add(item);
            _counts.NoteEvents++;
            Mark(time);
        }

        private void RecordHeight(float height)
        {
            _batch.HeightEvents.Add(new ReplayHeightEvent { Height = height, TimeSeconds = _audio?.songTime ?? 0 });
            _counts.HeightEvents++;
            Mark(_audio?.songTime ?? 0);
        }

        private void Mark(float time)
        {
            if (_eventCount == 0)
            {
                _batch.MinTimeSeconds = time;
                _batch.MaxTimeSeconds = time;
                _batchStartedAt = Time.realtimeSinceStartup;
            }
            else
            {
                _batch.MinTimeSeconds = Math.Min(_batch.MinTimeSeconds, time);
                _batch.MaxTimeSeconds = Math.Max(_batch.MaxTimeSeconds, time);
            }
            _eventCount++;
        }

        private void Flush()
        {
            if (_eventCount == 0) return;
            Send(new ReplayStreamPacket
            {
                StreamId = _streamId,
                Chunk = new ReplayChunk
                {
                    Cursor = Cursor(_sequence++, _batch.MaxTimeSeconds),
                    Events = _batch,
                    CumulativeEventCounts = CloneCounts()
                }
            });
            _chunkCount++;
            _batch = new StreamReplayEventBatch();
            _eventCount = 0;
        }

        private void Finish(ReplayCompletion completion = ReplayCompletion.ReplayCompletionAborted, LevelCompletionResults results = null)
        {
            if (!_started || _ended) return;
            _ended = true;
            Flush();
            Send(new ReplayStreamPacket
            {
                StreamId = _streamId,
                End = new ReplayStreamEnd
                {
                    Cursor = Cursor(_sequence++, _lastTime),
                    Completion = completion,
                    ChunkCount = _chunkCount,
                    CumulativeEventCounts = CloneCounts(),
                    Score = new ReplayScoreSummary
                    {
                        Score = (uint)Math.Max(0, results?.multipliedScore ?? _score?.multipliedScore ?? 0),
                        ModifiedScore = (uint)Math.Max(0, results?.modifiedScore ?? _score?.modifiedScore ?? 0),
                        MaxScore = (uint)Math.Max(0, _score?.immediateMaxPossibleModifiedScore ?? 0),
                        Combo = (uint)Math.Max(0, _lastCombo)
                    }
                }
            });
        }

        private void Send(ReplayStreamPacket packet) { _ = Client.SendReplayStream(packet); }
        private ReplayCursor Cursor(ulong sequence, float time) => new ReplayCursor
        {
            Sequence = sequence,
            SongTimeMs = (long)Math.Round(time * 1000),
            ClientTimeUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
        private ReplayEventCounts CloneCounts() => new ReplayEventCounts
        {
            PoseFrames = _counts.PoseFrames, HeightEvents = _counts.HeightEvents, NoteEvents = _counts.NoteEvents,
            ScoreEvents = _counts.ScoreEvents, ComboEvents = _counts.ComboEvents,
            MultiplierEvents = _counts.MultiplierEvents, EnergyEvents = _counts.EnergyEvents,
            PauseEvents = _counts.PauseEvents
        };

        private float PlayerHeight() => GetFloat(_playerTransforms, "playerHeadAndObstacleInteraction", "_playerHeight") is var h && h > 0 ? h : 1.7f;
        private float CurrentJumpDistance()
        {
            foreach (var item in Resources.FindObjectsOfTypeAll<UnityEngine.Object>())
            {
                if (!item.GetType().Name.Contains("MovementDataProvider")) continue;
                var value = GetFloat(item, "jumpDistance");
                if (value > 0) return value;
            }
            return 0;
        }
        private static object CurrentSceneSetup()
        {
            var installer = Resources.FindObjectsOfTypeAll<GameplayCoreInstaller>().FirstOrDefault();
            return GetMember(installer, "_sceneSetupData");
        }
        private Tuple<UnityEngine.Color, UnityEngine.Color> CurrentSaberColors()
        {
            foreach (var item in Resources.FindObjectsOfTypeAll<UnityEngine.Object>())
            {
                if (item.GetType().Name != "ColorManager") continue;
                var method = item.GetType().GetMethod("ColorForType", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (method == null) continue;
                try { return Tuple.Create((UnityEngine.Color)method.Invoke(item, new object[] { ColorType.ColorA }), (UnityEngine.Color)method.Invoke(item, new object[] { ColorType.ColorB })); }
                catch { }
            }
            return Tuple.Create(UnityEngine.Color.red, UnityEngine.Color.blue);
        }

        private static string NormalizeHash(string value) => (value ?? string.Empty).Replace("custom_level_", string.Empty).ToUpperInvariant();
        private static ReplayPose Pose(Vector3 p, Quaternion q) => new ReplayPose { Position = Vector(p), Rotation = new ReplayQuaternion { X = q.x, Y = q.y, Z = q.z, W = q.w } };
        private static ReplayQuaternion Rotation(Quaternion q) => new ReplayQuaternion { X = q.x, Y = q.y, Z = q.z, W = q.w };
        private static ReplayVector3 Vector(Vector3 v) => new ReplayVector3 { X = v.x, Y = v.y, Z = v.z };
        private static ReplayColor Color(UnityEngine.Color c) => new ReplayColor { R = c.r, G = c.g, B = c.b, A = c.a };
        private static bool IsBomb(NoteData data) => data.gameplayType == NoteData.GameplayType.Bomb;
        private static object GetMember(object target, params string[] names)
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
        private static int GetInt(object target, params string[] names) => ConvertValue(GetMember(target, names), 0);
        private static float GetFloat(object target, params string[] names) => ConvertValue(GetMember(target, names), 0f);
        private static bool GetBool(object target, params string[] names) => ConvertValue(GetMember(target, names), false);
        private static Vector3 GetVector(object target, params string[] names) => GetMember(target, names) is Vector3 value ? value : Vector3.zero;
        private static Quaternion GetQuaternion(object target, params string[] names) => GetMember(target, names) is Quaternion value ? value : Quaternion.identity;
        internal static string GetString(object target, params string[] names) => GetMember(target, names) as string ?? string.Empty;
        private static T ConvertValue<T>(object value, T fallback) { try { return value == null ? fallback : (T)Convert.ChangeType(value, typeof(T)); } catch { return fallback; } }

        public static void Complete(LevelCompletionResults results)
        {
            if (Instance == null) return;
            var completion = ReplayCompletion.ReplayCompletionAborted;
            if (results != null)
            {
                completion = results.levelEndStateType == LevelCompletionResults.LevelEndStateType.Cleared
                    ? ReplayCompletion.ReplayCompletionPassed
                    : results.levelEndStateType == LevelCompletionResults.LevelEndStateType.Failed
                        ? ReplayCompletion.ReplayCompletionFailed
                        : ReplayCompletion.ReplayCompletionQuit;
                if (results.levelEndAction == LevelCompletionResults.LevelEndAction.Quit)
                    completion = ReplayCompletion.ReplayCompletionQuit;
            }
            Instance.Finish(completion, results);
        }

        public static void Destroy() { if (Instance != null) UnityEngine.Object.Destroy(Instance.gameObject); }
        private void OnDestroy()
        {
            if (_beatmapObjects != null)
            {
                _beatmapObjects.noteWasCutEvent -= CollectCutInfo;
            }
            if (_score != null) _score.scoringForNoteFinishedEvent -= ScoringFinished;
            Finish();
            if (Instance == this) Instance = null;
        }
    }
}
