using System.Numerics;
using TournamentAssistantShared.Tarf;
using TournamentAssistantShared.Models.Tarf;
using static TournamentAssistantShared.Tarf.TarfValues;

namespace TARF.ScoreSaber
{
    public static class ScoreSaberConverter
    {
        public static TarfReplay ToTarf(ScoreSaberReplay scoreSaber, MotionEncoding motionEncoding)
        {
            var metadata = scoreSaber.Metadata;
            var playSettings = scoreSaber.PlaySettings;

            var environment = string.IsNullOrEmpty(playSettings?.Environment) ? metadata.Environment : playSettings.Environment;

            var header = new Header
            {
                Recorder = new Recorder { Source = ReplaySource.ReplaySourceScoresaber, SourceVersion = metadata.Version },
                Player = new Player { Platform = metadata.Platform, GameVersion = metadata.GameVersion },
                Map = new Map
                {
                    Hash = HashFromLevelId(metadata.LevelId),
                    LevelId = metadata.LevelId,
                    Characteristic = metadata.Characteristic,
                    Difficulty = DifficultyFromRank(metadata.Difficulty),
                    Environment = environment
                },
                Settings = new PlaySettings
                {
                    LeftHanded = metadata.LeftHanded,
                    PlayerHeight = metadata.InitialHeight,
                    NoteSpawnOffset = metadata.NoteSpawnOffset,
                    RoomRotation = metadata.RoomRotation,
                    RoomCenter = ToProto(metadata.RoomCenter)
                },
                MotionEncoding = motionEncoding
            };
            header.Settings.Modifiers.AddRange(metadata.Modifiers);

            if (playSettings != null)
            {
                ApplyPlaySettings(header, playSettings);
            }

            if (scoreSaber.ControllerOffsets != null)
            {
                header.ControllerOffsets = new ControllerOffsets
                {
                    Shared = ToControllerOffset(scoreSaber.ControllerOffsets.Shared),
                    Left = ToControllerOffset(scoreSaber.ControllerOffsets.Left),
                    Right = ToControllerOffset(scoreSaber.ControllerOffsets.Right)
                };
            }

            foreach (var extension in scoreSaber.OtherExtensions)
            {
                header.SourceExtensions.Add(new SourceExtension { Id = extension.Id, Version = (uint)extension.Version, Payload = extension.Payload });
            }

            var replay = new TarfReplay { Header = header };

            replay.Frames.AddRange(scoreSaber.PoseFrames.Select(frame => new TarfFrame
            {
                TimeUs = ToTimeUs(frame.Time),
                Fps = frame.Fps,
                Head = frame.Head,
                Left = frame.Left,
                Right = frame.Right
            }));

            replay.Events.AddRange(scoreSaber.NoteEvents.Select(ToNoteEvent));
            replay.Events.AddRange(scoreSaber.WallEvents.Select(ToWallEvent));
            replay.Events.AddRange(scoreSaber.HeightEvents.Select(height => new Event
            {
                TimeUs = ToTimeUs(height.Time),
                Height = new HeightEvent { Height = height.Height }
            }));
            replay.Events.AddRange(scoreSaber.PauseEvents.Select(pause => new Event
            {
                TimeUs = ToTimeUs(pause.Time),
                Pause = new PauseEvent
                {
                    DurationMs = pause.DurationMs,
                    StartedAtUnixMs = pause.UnixStartTimeMs,
                    EndedAtUnixMs = pause.UnixEndTimeMs
                }
            }));
            replay.Events.AddRange(scoreSaber.ScoreEvents.Select(ToScoreEvent));
            replay.Events.AddRange(scoreSaber.ComboEvents.Select(combo => new Event
            {
                TimeUs = ToTimeUs(combo.Time),
                Combo = new ComboEvent { Combo = combo.Combo }
            }));
            replay.Events.AddRange(scoreSaber.MultiplierEvents.Select(multiplier => new Event
            {
                TimeUs = ToTimeUs(multiplier.Time),
                Multiplier = new MultiplierEvent { Multiplier = multiplier.Multiplier, NextMultiplierProgress = multiplier.NextMultiplierProgress }
            }));
            replay.Events.AddRange(scoreSaber.EnergyEvents.Select(energy => new Event
            {
                TimeUs = ToTimeUs(energy.Time),
                Energy = new EnergyEvent { Energy = energy.Energy }
            }));

            var failed = metadata.FailTime > 0;
            replay.Footer = new Footer { Completion = failed ? Completion.CompletionFailed : Completion.CompletionPassed };
            if (failed) replay.Footer.EndTimeUs = ToTimeUs(metadata.FailTime);
            if (scoreSaber.ScoreEvents.Count > 0) replay.Footer.FinalScore = scoreSaber.ScoreEvents.OrderBy(x => x.Time).Last().Score;

            return replay;
        }

        private static void ApplyPlaySettings(Header header, ScoreSaberPlaySettings playSettings)
        {
            var settings = header.Settings;
            settings.SongSpeed = playSettings.SongSpeed;
            settings.JumpDistance = playSettings.JumpDistance;
            settings.EnvironmentEffectsFilterDefaultPreset = playSettings.EnvironmentEffectsFilterDefaultPreset;
            settings.EnvironmentEffectsFilterExpertPlusPreset = playSettings.EnvironmentEffectsFilterExpertPlusPreset;
            settings.EnvironmentEffectsFilterPreset = playSettings.EnvironmentEffectsFilterPreset;
            settings.NoTextsAndHuds = playSettings.NoTextsAndHuds;
            settings.SaberTrailIntensity = playSettings.SaberTrailIntensity;
            settings.HideNoteSpawnEffect = playSettings.HideNoteSpawnEffect;
            settings.ArcsHapticFeedback = playSettings.ArcsHapticFeedback;
            settings.ArcVisibility = playSettings.ArcVisibility;

            header.Colors = new ColorScheme
            {
                SaberA = ToColor(playSettings.LeftSaberColor),
                SaberB = ToColor(playSettings.RightSaberColor),
                Obstacle = ToColor(playSettings.ObstacleColor),
                Environment0 = ToColor(playSettings.EnvironmentColor0),
                Environment1 = ToColor(playSettings.EnvironmentColor1),
                EnvironmentWhite = ToColor(playSettings.EnvironmentColorW),
                Environment0Boost = ToColor(playSettings.EnvironmentColor0Boost),
                Environment1Boost = ToColor(playSettings.EnvironmentColor1Boost),
                EnvironmentWhiteBoost = ToColor(playSettings.EnvironmentColorWBoost),
                SupportsEnvironmentBoost = playSettings.SupportsEnvironmentColorBoost
            };
        }

        private static Event ToNoteEvent(ScoreSaberNoteEvent note)
        {
            var noteId = note.NoteId;
            var identity = new NoteIdentity
            {
                Time = noteId.Time,
                LineIndex = noteId.LineIndex,
                LineLayer = noteId.LineLayer,
                ColorType = noteId.ColorType,
                CutDirection = noteId.CutDirection
            };
            if (noteId.ScoringType.HasValue) identity.ScoringType = noteId.ScoringType.Value;
            if (noteId.GameplayType.HasValue) identity.GameplayType = noteId.GameplayType.Value;
            if (noteId.CutDirectionAngleOffset.HasValue) identity.CutDirectionAngleOffset = noteId.CutDirectionAngleOffset.Value;

            var noteEvent = new NoteEvent
            {
                Note = identity,
                Kind = (NoteEventKind)(int)note.EventType,
                UnityTimescale = note.UnityTimescale,
                TimeSyncTimescale = note.TimeSyncTimescale
            };
            if (note.WorldRotation.HasValue) noteEvent.WorldRotation = ToProto(note.WorldRotation.Value);
            if (note.InverseWorldRotation.HasValue) noteEvent.InverseWorldRotation = ToProto(note.InverseWorldRotation.Value);
            if (note.NoteRotation.HasValue) noteEvent.NoteRotation = ToProto(note.NoteRotation.Value);
            if (note.NotePosition.HasValue) noteEvent.NotePosition = ToProto(note.NotePosition.Value);

            if (note.EventType == ScoreSaberNoteEventType.GoodCut || note.EventType == ScoreSaberNoteEventType.BadCut)
            {
                noteEvent.Cut = new NoteCut
                {
                    DirectionOk = note.DirectionOk,
                    SaberSpeed = note.SaberSpeed,
                    SaberDirection = ToProto(note.SaberDirection),
                    SaberType = note.SaberType,
                    CutDirectionDeviation = note.CutDirectionDeviation,
                    CutPoint = ToProto(note.CutPoint),
                    CutNormal = ToProto(note.CutNormal),
                    CutDistanceToCenter = note.CutDistanceToCenter,
                    CutAngle = note.CutAngle,
                    BeforeCutRating = note.BeforeCutRating,
                    AfterCutRating = note.AfterCutRating
                };
                if (note.TimeDeviation.HasValue) noteEvent.Cut.TimeDeviation = note.TimeDeviation.Value;
            }

            return new Event { TimeUs = ToTimeUs(note.Time), Note = noteEvent };
        }

        private static Event ToWallEvent(ScoreSaberWallEvent wall)
        {
            return new Event
            {
                TimeUs = ToTimeUs(wall.Time),
                Wall = new WallEvent
                {
                    Wall = new WallIdentity
                    {
                        Time = wall.ObstacleTime,
                        Duration = wall.ObstacleDuration,
                        LineIndex = wall.LineIndex,
                        LineLayer = wall.LineLayer,
                        Width = wall.Width,
                        Height = wall.Height
                    },
                    Energy = wall.Energy,
                    ExitTimeUs = ToTimeUs(wall.ExitTime)
                }
            };
        }

        private static Event ToScoreEvent(ScoreSaberScoreEvent score)
        {
            var scoreEvent = new ScoreEvent { Score = score.Score };
            if (score.ImmediateMaxPossibleScore.HasValue) scoreEvent.ImmediateMaxPossibleScore = score.ImmediateMaxPossibleScore.Value;
            return new Event { TimeUs = ToTimeUs(score.Time), Score = scoreEvent };
        }

        // ScoreSaber stores difficulties as 1, 3, 5, 7 and 9.
        private static Difficulty DifficultyFromRank(int rank)
        {
            switch (rank)
            {
                case 1: return Difficulty.DifficultyEasy;
                case 3: return Difficulty.DifficultyNormal;
                case 5: return Difficulty.DifficultyHard;
                case 7: return Difficulty.DifficultyExpert;
                case 9: return Difficulty.DifficultyExpertPlus;
                default: return Difficulty.DifficultyUnspecified;
            }
        }

        private static ControllerOffset ToControllerOffset(ScoreSaberControllerOffset offset)
        {
            if (offset == null) return null;
            return new ControllerOffset { Position = ToProto(offset.Position), EulerDegrees = ToProto(offset.EulerRotation) };
        }

        private static Color ToColor(Vector4? color)
        {
            if (!color.HasValue) return null;
            return new Color { R = color.Value.X, G = color.Value.Y, B = color.Value.Z, A = color.Value.W };
        }
    }
}
