namespace TournamentAssistantShared.Tarf
{
    // The CRC-32 used by zlib and PNG
    public static class Crc32
    {
        private static readonly uint[] Table = CreateTable();

        public static uint Compute(byte[] data)
        {
            var crc = 0xFFFFFFFFu;
            foreach (var value in data)
            {
                crc = Table[(crc ^ value) & 0xFF] ^ (crc >> 8);
            }
            return crc ^ 0xFFFFFFFFu;
        }

        private static uint[] CreateTable()
        {
            var table = new uint[256];
            for (uint index = 0; index < table.Length; index++)
            {
                var value = index;
                for (var bit = 0; bit < 8; bit++)
                {
                    value = (value & 1) != 0 ? 0xEDB88320u ^ (value >> 1) : value >> 1;
                }
                table[index] = value;
            }
            return table;
        }
    }
}
