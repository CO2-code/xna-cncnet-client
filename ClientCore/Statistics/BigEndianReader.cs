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

        public byte ReadByte() => _br.ReadByte();

        public uint ReadUInt32()
        {
            var data = _br.ReadBytes(4);
            return ((uint)data[0] << 24) | ((uint)data[1] << 16) | ((uint)data[2] << 8) | data[3];
        }

        // Reads 4 bytes, returns the first as an int, discards the rest.
        // Matches Read_Byte() semantics in the standalone parser.
        public int ReadByteAs4()
        {
            int value = _br.ReadByte();
            _br.ReadBytes(3);
            return value;
        }
    }
}
