using System.IO;

namespace ClientCore.Statistics
{
    /// <summary>
    /// Helper to read big‑endian values from a binary stream.
    /// </summary>
    public class BigEndianReader
    {
        private readonly BinaryReader _br;
        public Stream BaseStream => _br.BaseStream;

        public BigEndianReader(BinaryReader br)
        {
            _br = br;
        }

        public short ReadInt16()
        {
            var data = _br.ReadBytes(2);
            return (short)((data[0] << 8) | data[1]);
        }

        public int ReadInt32()
        {
            var data = _br.ReadBytes(4);
            return (data[0] << 24) | (data[1] << 16) | (data[2] << 8) | data[3];
        }

        public byte[] ReadBytes(int count) => _br.ReadBytes(count);
    }
}