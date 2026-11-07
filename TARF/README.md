# TARF - TournamentAssistant Replay Format

TournamentAssistant replay format.

## TARF V1

### Details

TARF stands for TournamentAssistant Replay Format. TournamentAssistant records it during tournament plays. It contains player movement, scoring, the player's colors and HitScoreVisualizer config, their mods, and the mid-play modifiers TA can toggle. It can also carry the map itself.

It is based on [BSOR, the Beat Saber Open Replay format](https://github.com/BeatLeader/BS-Open-Replay). Unlike BSOR, movement is stored as quantized deltas between anchor frames, which makes a recording about half the size of the same BSOR file while staying accurate to 0.01 mm.

You can play TARF files in [chroviewer](https://github.com/ServerBP/chroviewer) by opening the file, or with `?tarfPath=<path or url>`.

To record replays, turn on "Enable Replay Recording" in the tournament settings. Players need the TournamentAssistant plugin on PC or TournamentAssistant Standalone on Quest.

### Implementations

- C# implementation: `TournamentAssistantShared/Tarf`
- C++ implementation (writing only): `TournamentAssistantStandalone/src/TA/Tarf.cpp`
- TypeScript implementation (reading only): `src/core/replay/parse-tarf.ts` in chroviewer

### Structure

```
"TARF"                         - 4 bytes, magic.
1                              - ushort, major version.
0                              - ushort, minor version.

{                              - Record, repeated until the end of the file.
  type                         - byte, 1 header, 2 motion block, 3 event batch, 4 footer.
  codec                        - byte, 0 stored as is, 1 raw deflate.
  0                            - ushort, reserved.
  storedLength                 - uint, bytes that follow this record header.
  decodedLength                - uint, payload length after decompression.
  crc32                        - uint, CRC-32 of the decoded payload.
  payload                      - storedLength bytes, a protobuf message from tarf.proto.
}
```

The first record is always the `Header`: player, map (hash, characteristic, difficulty and optionally a zip of the map with only that difficulty), settings, colors, HSV config, mods and the motion precision. The last record is the `Footer` with the result. It is missing if the recording was cut off. In between, `MotionBlock` and `EventBatch` records alternate. Unknown record types are skipped.

```
MotionBlock.frames             - frame_count frames, each 23 ZigZag varints, in this order:
{
  time                         - song time, microseconds.
  fps                          - player's FPS.
  {x, y, z}                    - head position, in steps of motion_encoding.position_step_nm nanometers.
  {x, y, z, w}                 - head rotation, in steps of motion_encoding.rotation_step_nano billionths.
  {x, y, z}                    - left hand position.
  {x, y, z, w}                 - left hand rotation.
  {x, y, z}                    - right hand position.
  {x, y, z, w}                 - right hand rotation.
}
```

Each value is stored as its difference from a prediction. The first frame of a block (the anchor) is predicted as 0, the second as the value in the first frame, and every later frame as `2 * previous - before_previous`. Every block decodes on its own.

Events (notes, walls, heights, pauses, score, combo, multiplier, energy and mid-play modifiers) are in `EventBatch` records, each with its song time in microseconds. Readers sort them by time, since a live recording writes a note only once its swing has finished.

### .tarf encoding details

Integers in the file and record headers are little endian. Payloads use regular protobuf encoding, see `tarf.proto`. Times are microseconds of song time. Positions are meters and rotations are quaternions, like BSOR.

The default steps are 10000 nm (0.01 mm) for positions and 1000 billionths for quaternion components. Decoded quaternions should be normalized before use.

## The converter

Converts BeatLeader (`.bsor`) and ScoreSaber (`.dat`) replays to TARF, and TARF back to BSOR.

```
dotnet run --project TARF -- encode <replay.bsor | replay.dat> [options]
    -output <path>            Where to write the .tarf file. Defaults to the input path with a .tarf extension.
    -hsv <path>               HitScoreVisualizer config (.json) to embed.
    -map <path>               Zip of the map to embed. Only the recorded difficulty is kept.
    -downloadMap true         Download the map from BeatSaver and embed the recorded difficulty.
    -positionStepNm <n>       Position precision in nanometers. Defaults to 10000 (0.01 mm).
    -rotationStepNano <n>     Quaternion precision in billionths. Defaults to 1000.
    -anchorInterval <n>       Frames per motion block, including its anchor frame. Defaults to 60.

dotnet run --project TARF -- decode <replay.tarf> [options]
    -output <path>            Where to write the .bsor file. Defaults to the input path with a .bsor extension.
    -hsv <path>               Also save the embedded HitScoreVisualizer config to this path.
    -map <path>               Also save the embedded map zip to this path.

dotnet run --project TARF -- info <replay.tarf>
```

For example, to convert a BeatLeader replay with an HSV config and its map:

```
dotnet run --project TARF -- encode replay.bsor -hsv EJ.json -downloadMap true
```
