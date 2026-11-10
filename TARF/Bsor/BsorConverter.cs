using TournamentAssistantShared.Tarf;
using TournamentAssistantShared.Models.Tarf;
using static TournamentAssistantShared.Tarf.TarfValues;

namespace TARF.Bsor
{
    public static class BsorConverter
    {
        // BSOR custom data entries are kept as source extensions named with this prefix and their key.
        private const string CustomDataExtensionPrefix = "bsor.custom-data/";

        public static TarfReplay ToTarf(BsorReplay bsor, MotionEncoding motionEncoding)
        {
            var info = bsor.Info;
            var mapHash = HashFromLevelId(info.Hash);

            var header = new Header
            {
                Recorder = new Recorder { Source = ReplaySource.ReplaySourceBsor, SourceVersion = info.ModVersion },
                RecordedAtUnixMs = long.TryParse(info.Timestamp, out var timestampSeconds) ? timestampSeconds * 1000 : 0,
                Player = new Player
                {
                    Id = info.PlayerId,
                    Name = info.PlayerName,
                    Platform = info.Platform,
                    GameVersion = info.GameVersion,
                    Hmd = info.Hmd,
                    Controller = info.Controller,
                    TrackingSystem = info.TrackingSystem
                },
                Map = new Map
                {
                    // BeatLeader stores the level id instead of a hash for maps that aren't custom.
                    Hash = mapHash,
                    LevelId = mapHash == "" ? info.Hash : "",
                    Characteristic = info.Mode,
                    Difficulty = DifficultyFromName(info.Difficulty),
                    SongName = info.SongName,
                    Mapper = info.Mapper,
                    Environment = info.Environment
                },
                Settings = new PlaySettings
                {
                    LeftHanded = info.LeftHanded,
                    PlayerHeight = info.Height,
                    JumpDistance = info.JumpDistance
                },
                MotionEncoding = motionEncoding
            };

            header.Settings.Modifiers.AddRange(info.Modifiers.Split(',', StringSplitOptions.RemoveEmptyEntries));

            if (info.Speed > 0 || info.StartTime > 0)
            {
                header.Settings.Practice = new Practice { StartTime = info.StartTime, SongSpeed = info.Speed };
            }

            if (bsor.SaberOffsets != null)
            {
                header.ControllerOffsets = new ControllerOffsets
                {
                    Left = ToControllerOffset(bsor.SaberOffsets.Left),
                    Right = ToControllerOffset(bsor.SaberOffsets.Right)
                };
            }

            foreach (var entry in bsor.CustomData)
            {
                header.SourceExtensions.Add(new SourceExtension { Id = CustomDataExtensionPrefix + entry.Key, Version = 1, Payload = entry.Value });
            }

            var replay = new TarfReplay { Header = header };

            replay.Frames.AddRange(bsor.Frames.Select(frame => new TarfFrame
            {
                TimeUs = ToTimeUs(frame.Time),
                Fps = frame.Fps,
                Head = frame.Head,
                Left = frame.LeftHand,
                Right = frame.RightHand
            }));

            replay.Events.AddRange(bsor.Notes.Select(ToNoteEvent));
            replay.Events.AddRange(bsor.Walls.Select(ToWallEvent));
            replay.Events.AddRange(bsor.Heights.Select(height => new Event
            {
                TimeUs = ToTimeUs(height.Time),
                Height = new HeightEvent { Height = height.Height }
            }));
            replay.Events.AddRange(bsor.Pauses.Select(pause => new Event
            {
                TimeUs = ToTimeUs(pause.Time),
                Pause = new PauseEvent { DurationMs = pause.DurationSeconds * 1000 }
            }));

            var failed = info.FailTime > 0;
            replay.Footer = new Footer
            {
                Completion = failed ? Completion.CompletionFailed : Completion.CompletionPassed,
                FinalScore = info.Score
            };
            if (failed) replay.Footer.EndTimeUs = ToTimeUs(info.FailTime);

            return replay;
        }

        public static BsorReplay FromTarf(TarfReplay tarf)
        {
            var header = tarf.Header;
            var player = header.Player ?? new Player();
            var map = header.Map ?? new Map();
            var settings = header.Settings ?? new PlaySettings();
            var footer = tarf.Footer;

            var info = new BsorInfo
            {
                ModVersion = header.Recorder?.Source == ReplaySource.ReplaySourceBsor ? header.Recorder.SourceVersion : header.Recorder?.Writer ?? "",
                GameVersion = player.GameVersion,
                Timestamp = (header.RecordedAtUnixMs / 1000).ToString(),

                PlayerId = player.Id,
                PlayerName = player.Name,
                Platform = player.Platform,

                TrackingSystem = player.TrackingSystem,
                Hmd = player.Hmd,
                Controller = player.Controller,

                Hash = map.Hash != "" ? map.Hash : map.LevelId,
                SongName = map.SongName,
                Mapper = map.Mapper,
                Difficulty = DifficultyName(map.Difficulty),

                Score = footer?.FinalScore ?? 0,
                Mode = map.Characteristic,
                Environment = map.Environment,
                Modifiers = string.Join(",", settings.Modifiers),
                JumpDistance = settings.JumpDistance,
                LeftHanded = settings.LeftHanded,
                Height = settings.PlayerHeight,

                StartTime = settings.Practice?.StartTime ?? 0,
                FailTime = footer?.Completion == Completion.CompletionFailed ? ToSeconds(footer.EndTimeUs) : 0,
                Speed = settings.Practice?.SongSpeed ?? 0
            };

            var bsor = new BsorReplay { Info = info };

            bsor.Frames.AddRange(tarf.Frames.Select(frame => new BsorFrame
            {
                Time = ToSeconds(frame.TimeUs),
                Fps = frame.Fps,
                Head = frame.Head,
                LeftHand = frame.Left,
                RightHand = frame.Right
            }));

            foreach (var tarfEvent in tarf.Events.OrderBy(x => x.TimeUs))
            {
                if (tarfEvent.Note != null) bsor.Notes.Add(FromNoteEvent(tarfEvent));
                else if (tarfEvent.Wall != null) bsor.Walls.Add(FromWallEvent(tarfEvent));
                else if (tarfEvent.Height != null) bsor.Heights.Add(new BsorHeightEvent { Height = tarfEvent.Height.Height, Time = ToSeconds(tarfEvent.TimeUs) });
                else if (tarfEvent.Pause != null) bsor.Pauses.Add(new BsorPauseEvent { DurationSeconds = tarfEvent.Pause.DurationMs / 1000, Time = ToSeconds(tarfEvent.TimeUs) });
            }

            var offsets = header.ControllerOffsets;
            if (offsets?.Left?.Quaternion != null && offsets.Right?.Quaternion != null)
            {
                bsor.SaberOffsets = new BsorSaberOffsets
                {
                    Left = FromControllerOffset(offsets.Left),
                    Right = FromControllerOffset(offsets.Right)
                };
            }

            foreach (var extension in header.SourceExtensions.Where(x => x.Id.StartsWith(CustomDataExtensionPrefix)))
            {
                bsor.CustomData[extension.Id.Substring(CustomDataExtensionPrefix.Length)] = extension.Payload ?? Array.Empty<byte>();
            }

            return bsor;
        }

        private static Event ToNoteEvent(BsorNoteEvent note)
        {
            var identity = DecodeNoteId(note.NoteId);
            identity.Time = note.SpawnTime;

            var noteEvent = new NoteEvent { Note = identity, Kind = ToNoteEventKind(note.EventType) };

            var cut = note.CutInfo;
            if (cut != null)
            {
                noteEvent.Cut = new NoteCut
                {
                    SpeedOk = cut.SpeedOk,
                    DirectionOk = cut.DirectionOk,
                    SaberTypeOk = cut.SaberTypeOk,
                    WasCutTooSoon = cut.WasCutTooSoon,
                    SaberSpeed = cut.SaberSpeed,
                    SaberDirection = ToProto(cut.SaberDirection),
                    SaberType = cut.SaberType,
                    TimeDeviation = cut.TimeDeviation,
                    CutDirectionDeviation = cut.CutDirectionDeviation,
                    CutPoint = ToProto(cut.CutPoint),
                    CutNormal = ToProto(cut.CutNormal),
                    CutDistanceToCenter = cut.CutDistanceToCenter,
                    CutAngle = cut.CutAngle,
                    BeforeCutRating = cut.BeforeCutRating,
                    AfterCutRating = cut.AfterCutRating
                };
            }

            return new Event { TimeUs = ToTimeUs(note.EventTime), Note = noteEvent };
        }

        private static BsorNoteEvent FromNoteEvent(Event tarfEvent)
        {
            var noteEvent = tarfEvent.Note;
            var identity = noteEvent.Note ?? new NoteIdentity();

            var note = new BsorNoteEvent
            {
                NoteId = identity.ShouldSerializeBsorNoteId() ? identity.BsorNoteId : EncodeNoteId(identity),
                EventTime = ToSeconds(tarfEvent.TimeUs),
                SpawnTime = identity.Time,
                EventType = FromNoteEventKind(noteEvent.Kind)
            };

            var isCut = note.EventType == BsorNoteEventType.Good || note.EventType == BsorNoteEventType.Bad;
            if (isCut)
            {
                // ScoreSaber doesn't record every flag BSOR has. A good cut means all of
                // them passed, and for bad cuts the saber type check can be worked out.
                var cut = noteEvent.Cut ?? new NoteCut();
                note.CutInfo = new BsorNoteCutInfo
                {
                    SpeedOk = cut.ShouldSerializeSpeedOk() ? cut.SpeedOk : true,
                    DirectionOk = cut.DirectionOk,
                    SaberTypeOk = cut.ShouldSerializeSaberTypeOk() ? cut.SaberTypeOk : cut.SaberType == identity.ColorType,
                    WasCutTooSoon = cut.ShouldSerializeWasCutTooSoon() && cut.WasCutTooSoon,
                    SaberSpeed = cut.SaberSpeed,
                    SaberDirection = FromProto(cut.SaberDirection),
                    SaberType = cut.SaberType,
                    TimeDeviation = cut.TimeDeviation,
                    CutDirectionDeviation = cut.CutDirectionDeviation,
                    CutPoint = FromProto(cut.CutPoint),
                    CutNormal = FromProto(cut.CutNormal),
                    CutDistanceToCenter = cut.CutDistanceToCenter,
                    CutAngle = cut.CutAngle,
                    BeforeCutRating = cut.BeforeCutRating,
                    AfterCutRating = cut.AfterCutRating
                };
            }

            return note;
        }

        // BeatLeader packs a note into one integer:
        //   (scoringType + 2) * 10000 + lineIndex * 1000 + lineLayer * 100 + colorType * 10 + cutDirection
        // Replays from before scoring types were recorded leave that part as 0.
        // Ids of 100000 and above come from maps with extended line indices, which
        // move the first three parts up by three digits.
        private static NoteIdentity DecodeNoteId(int noteId)
        {
            var scoringTypeDivisor = noteId < 100_000 ? 10_000 : 10_000_000;
            var lineIndexDivisor = scoringTypeDivisor / 10;
            var lineLayerDivisor = lineIndexDivisor / 10;

            var identity = new NoteIdentity
            {
                LineIndex = noteId % scoringTypeDivisor / lineIndexDivisor,
                LineLayer = noteId % lineIndexDivisor / lineLayerDivisor,
                ColorType = noteId % 100 / 10,
                CutDirection = noteId % 10,
                BsorNoteId = noteId
            };

            var encodedScoringType = noteId / scoringTypeDivisor;
            if (encodedScoringType != 0) identity.ScoringType = encodedScoringType - 2;

            return identity;
        }

        private static int EncodeNoteId(NoteIdentity identity)
        {
            var encodedScoringType = identity.ShouldSerializeScoringType() ? identity.ScoringType + 2 : 0;
            return encodedScoringType * 10000
                + identity.LineIndex * 1000
                + identity.LineLayer * 100
                + identity.ColorType * 10
                + identity.CutDirection;
        }

        // BeatLeader packs a wall into lineIndex * 100 + obstacleType * 10 + width.
        private static Event ToWallEvent(BsorWallEvent wall)
        {
            return new Event
            {
                TimeUs = ToTimeUs(wall.Time),
                Wall = new WallEvent
                {
                    Wall = new WallIdentity
                    {
                        Time = wall.SpawnTime,
                        LineIndex = wall.WallId / 100,
                        Width = wall.WallId % 10,
                        BsorWallId = wall.WallId
                    },
                    Energy = wall.Energy
                }
            };
        }

        private static BsorWallEvent FromWallEvent(Event tarfEvent)
        {
            var wall = tarfEvent.Wall.Wall ?? new WallIdentity();
            return new BsorWallEvent
            {
                WallId = wall.ShouldSerializeBsorWallId() ? wall.BsorWallId : wall.LineIndex * 100 + wall.Width,
                Energy = tarfEvent.Wall.Energy,
                Time = ToSeconds(tarfEvent.TimeUs),
                SpawnTime = wall.Time
            };
        }

        private static NoteEventKind ToNoteEventKind(BsorNoteEventType eventType)
        {
            switch (eventType)
            {
                case BsorNoteEventType.Good: return NoteEventKind.NoteEventKindGoodCut;
                case BsorNoteEventType.Bad: return NoteEventKind.NoteEventKindBadCut;
                case BsorNoteEventType.Miss: return NoteEventKind.NoteEventKindMiss;
                case BsorNoteEventType.Bomb: return NoteEventKind.NoteEventKindBomb;
                default: return NoteEventKind.NoteEventKindUnspecified;
            }
        }

        private static BsorNoteEventType FromNoteEventKind(NoteEventKind kind)
        {
            switch (kind)
            {
                case NoteEventKind.NoteEventKindGoodCut: return BsorNoteEventType.Good;
                case NoteEventKind.NoteEventKindBadCut: return BsorNoteEventType.Bad;
                case NoteEventKind.NoteEventKindBomb: return BsorNoteEventType.Bomb;
                default: return BsorNoteEventType.Miss;
            }
        }

        private static ControllerOffset ToControllerOffset(TarfPose pose)
        {
            return new ControllerOffset { Position = ToProto(pose.Position), Quaternion = ToProto(pose.Rotation) };
        }

        private static TarfPose FromControllerOffset(ControllerOffset offset)
        {
            return new TarfPose { Position = FromProto(offset.Position), Rotation = FromProto(offset.Quaternion) };
        }
    }
}
