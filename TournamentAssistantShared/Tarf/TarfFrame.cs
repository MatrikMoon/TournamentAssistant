namespace TournamentAssistantShared.Tarf
{
    // Plain value types instead of System.Numerics, which Unity's runtime doesn't always have
    public struct TarfVector3
    {
        public float X;
        public float Y;
        public float Z;

        public TarfVector3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }

    public struct TarfQuaternion
    {
        public float X;
        public float Y;
        public float Z;
        public float W;

        public static TarfQuaternion Identity => new TarfQuaternion(0, 0, 0, 1);

        public TarfQuaternion(float x, float y, float z, float w)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }
    }

    public struct TarfPose
    {
        public TarfVector3 Position;
        public TarfQuaternion Rotation;

        public TarfPose(TarfVector3 position, TarfQuaternion rotation)
        {
            Position = position;
            Rotation = rotation;
        }
    }

    public struct TarfFrame
    {
        public long TimeUs;
        public int Fps;
        public TarfPose Head;
        public TarfPose Left;
        public TarfPose Right;
    }
}
