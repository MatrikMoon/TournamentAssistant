using System;
using System.Collections.Generic;
using System.IO;
using TournamentAssistantShared.Models.Tarf;

namespace TournamentAssistantShared.Tarf
{
    // Packs motion frames into MotionBlocks and back, as described in TARF/README.md
    //
    // Every frame becomes a fixed list of integers: time, fps, then the quantized position and rotation of the head, left hand and right hand
    // Each integer is written as its difference from a prediction made from the two frames before it:
    //   first frame (the anchor): predicted as 0, so the value is stored as is
    //   second frame:             predicted to equal the first frame
    //   later frames:             predicted to keep moving at the same speed
    public static class MotionCodec
    {
        public const int ValuesPerFrame = 2 + 3 * PoseValueCount;

        private const int PoseValueCount = 7;
        private const double NanoUnitsPerUnit = 1_000_000_000d;

        // Packs every frame in the list into one block. firstFrameIndex is the index of the first frame within the whole recording
        public static MotionBlock Pack(IReadOnlyList<TarfFrame> frames, uint firstFrameIndex, MotionEncoding encoding)
        {
            var frameCount = frames.Count;
            using var packed = new MemoryStream(frameCount * ValuesPerFrame * 2);

            // Three buffers take turns being the current, previous and before previous frame, so packing allocates nothing per frame
            var current = new long[ValuesPerFrame];
            var previous = new long[ValuesPerFrame];
            var beforePrevious = new long[ValuesPerFrame];

            for (var i = 0; i < frameCount; i++)
            {
                Quantize(frames[i], i > 0 ? previous : null, encoding, current);

                for (var value = 0; value < ValuesPerFrame; value++)
                {
                    VarInt.WriteSigned(packed, current[value] - Predict(i, previous, beforePrevious, value));
                }

                var oldest = beforePrevious;
                beforePrevious = previous;
                previous = current;
                current = oldest;
            }

            return new MotionBlock
            {
                FirstFrameIndex = firstFrameIndex,
                FrameCount = (uint)frameCount,
                Frames = packed.ToArray()
            };
        }

        public static List<TarfFrame> Unpack(MotionBlock block, MotionEncoding encoding)
        {
            var frames = new List<TarfFrame>((int)block.FrameCount);
            var packed = block.Frames ?? Array.Empty<byte>();
            var offset = 0;

            var current = new long[ValuesPerFrame];
            var previous = new long[ValuesPerFrame];
            var beforePrevious = new long[ValuesPerFrame];

            for (var i = 0; i < block.FrameCount; i++)
            {
                for (var value = 0; value < ValuesPerFrame; value++)
                {
                    current[value] = Predict(i, previous, beforePrevious, value) + VarInt.ReadSigned(packed, ref offset);
                }

                frames.Add(Dequantize(current, encoding));

                var oldest = beforePrevious;
                beforePrevious = previous;
                previous = current;
                current = oldest;
            }

            if (offset != packed.Length) throw new InvalidDataException("Motion block has trailing data");
            return frames;
        }

        private static long Predict(int frameIndex, long[] previous, long[] beforePrevious, int value)
        {
            if (frameIndex == 0) return 0;
            if (frameIndex == 1) return previous[value];
            return 2 * previous[value] - beforePrevious[value];
        }

        private static void Quantize(TarfFrame frame, long[] previous, MotionEncoding encoding, long[] values)
        {
            values[0] = frame.TimeUs;
            values[1] = frame.Fps;
            QuantizePose(frame.Head, values, 2, previous, encoding);
            QuantizePose(frame.Left, values, 2 + PoseValueCount, previous, encoding);
            QuantizePose(frame.Right, values, 2 + 2 * PoseValueCount, previous, encoding);
        }

        private static void QuantizePose(TarfPose pose, long[] values, int start, long[] previous, MotionEncoding encoding)
        {
            var positionStep = encoding.PositionStepNm / NanoUnitsPerUnit;
            values[start] = (long)Math.Round(pose.Position.X / positionStep);
            values[start + 1] = (long)Math.Round(pose.Position.Y / positionStep);
            values[start + 2] = (long)Math.Round(pose.Position.Z / positionStep);

            // q and -q are the same rotation. Keeping the sign consistent with the previous frame avoids huge deltas when the game flips it
            var rotation = pose.Rotation;
            if (previous != null && SignAgreement(rotation, previous, start + 3) < 0)
            {
                rotation = new TarfQuaternion(-rotation.X, -rotation.Y, -rotation.Z, -rotation.W);
            }

            var rotationStep = encoding.RotationStepNano / NanoUnitsPerUnit;
            values[start + 3] = (long)Math.Round(rotation.X / rotationStep);
            values[start + 4] = (long)Math.Round(rotation.Y / rotationStep);
            values[start + 5] = (long)Math.Round(rotation.Z / rotationStep);
            values[start + 6] = (long)Math.Round(rotation.W / rotationStep);
        }

        private static double SignAgreement(TarfQuaternion rotation, long[] previous, int start)
        {
            return rotation.X * (double)previous[start]
                + rotation.Y * (double)previous[start + 1]
                + rotation.Z * (double)previous[start + 2]
                + rotation.W * (double)previous[start + 3];
        }

        private static TarfFrame Dequantize(long[] values, MotionEncoding encoding)
        {
            return new TarfFrame
            {
                TimeUs = values[0],
                Fps = (int)values[1],
                Head = DequantizePose(values, 2, encoding),
                Left = DequantizePose(values, 2 + PoseValueCount, encoding),
                Right = DequantizePose(values, 2 + 2 * PoseValueCount, encoding)
            };
        }

        private static TarfPose DequantizePose(long[] values, int start, MotionEncoding encoding)
        {
            var positionStep = encoding.PositionStepNm / NanoUnitsPerUnit;
            var rotationStep = encoding.RotationStepNano / NanoUnitsPerUnit;

            return new TarfPose(
                new TarfVector3(
                    (float)(values[start] * positionStep),
                    (float)(values[start + 1] * positionStep),
                    (float)(values[start + 2] * positionStep)),
                new TarfQuaternion(
                    (float)(values[start + 3] * rotationStep),
                    (float)(values[start + 4] * rotationStep),
                    (float)(values[start + 5] * rotationStep),
                    (float)(values[start + 6] * rotationStep)));
        }
    }
}
