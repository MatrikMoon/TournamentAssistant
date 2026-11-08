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
- C++ implementation (writing only): `TournamentAssistantStandalone/src/TA/Tarf.cpp` and `PackedJson.cpp`
- TypeScript implementation (reading only): `src/core/replay/parse-tarf.ts` and `packed-json.ts` in chroviewer

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

The first record is always the `Header`: player, map (hash, characteristic, difficulty and optionally the map's files for that difficulty), settings, colors, HSV config, mods and the motion precision. The last record is the `Footer` with the result. It is missing if the recording was cut off. In between, `MotionBlock` and `EventBatch` records alternate. Unknown record types are skipped.

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

### Map files

`Map.files` holds what is needed to play the recorded difficulty: the Info.dat, the difficulty and its lightshow, the BPM data, the cover and the song. Each file is stored as it is, deflated, or as packed JSON, whichever is smallest.

Packed JSON stores the values of one key from every object of a list together, so note times, event types and colors each sit next to their own kind before the result is deflated. Map JSON ends up about 2 to 4 times smaller than its deflated text, lighting heavy maps up to 10 times. It keeps the values Beat Saber reads rather than the exact text: whole numbers stay exact, other numbers become the 32 bit float the game parses them to, and keys can change order.

```
stringCount                    - varint.
{
  length                       - varint.
  string                       - length bytes, UTF-8. Strings and keys are referenced by their index in this table.
}
root                           - a column with one value.
```

A column holds n values, where n is given by whatever contains the column.

```
kind                           - byte.
0 numbers {
  denominator                  - varint.
  hasForms                     - byte.
  forms                        - n bytes, only if hasForms is 1. 0 fraction, 1 float, 2 double. All fractions without it.
  numerators                   - integers, one per fraction.
  floats                       - 4 bytes each.
  doubles                      - 8 bytes each.
}
1 strings                      - n varint string indexes.
2 lists {
  lengths                      - integers.
  layout                       - byte, only if any list has items. 1 flat: one column of every item in order.
                                 0 positional: for each position p, a column of the items at p of the lists longer than p.
}
3 objects {
  grouped                      - byte.
  groupKey                     - varint string index, only if grouped is 1.
  groups                       - a column of the n group values, only if grouped is 1. The rows below are then stored
                                 in order of their group value, rows with the same value keeping their original order.
  keyCount                     - varint.
  {
    key                        - varint string index.
    presence                   - byte, 0 if every row has the key, 1 if a bitmap of ceil(n / 8) bytes follows.
                                 Row i has the key if bit i % 8 of byte i / 8 is set.
    values                     - a column of the values of the rows that have the key.
  }
}
4 literals                     - n bytes, 0 null, 1 false, 2 true.
5 mixed {
  kinds                        - n bytes, the kind of each value.
  columns                      - a column for each kind from 0 to 4 that appears, holding the values of that kind in order.
}

integers {
  delta                        - byte, 1 if each value is stored as its difference from the one before.
  values                       - ZigZag varints.
}
```

A fraction is `numerator / denominator`. When it divides evenly the value is that whole number, otherwise it is the 32 bit float nearest to the quotient.

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
    -map <path>               Also save the embedded map to this path as a zip.

dotnet run --project TARF -- info <replay.tarf>
```

For example, to convert a BeatLeader replay with an HSV config and its map:

```
dotnet run --project TARF -- encode replay.bsor -hsv EJ.json -downloadMap true
```
