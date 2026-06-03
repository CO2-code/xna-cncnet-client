using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Rampastring.Tools;

namespace ClientCore.Statistics
{
    /// <summary>
    /// Parses a CnCNet stats.dmp file and provides human‑readable output.
    /// </summary>
    public class StatsDumpParser
    {
        public BigEndianReader Bin;
        public int Pos;
        public int DumpSize = -1;
        public int ReportedSize = -1;

        public Dictionary<string, ParsedField> Results = new Dictionary<string, ParsedField>();

        public class ParsedField
        {
            public int Type;
            public int Length;
            public byte[] Raw;
        }

        public StatsDumpParser(string fileName)
        {
            Parse(fileName);
        }

        private void Parse(string fileName)
        {
            using (BinaryReader b = new BinaryReader(File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.Read)))
            {
                Bin = new BigEndianReader(b);
                Pos = 0;
                DumpSize = (int)Bin.BaseStream.Length;

                if (DumpSize < 4)
                    throw new Exception("File too small");

                // Header: reported size (2 bytes) + 2 unknown bytes
                ReportedSize = Bin.ReadInt16();
                Bin.ReadInt16();
                Pos += 4;

                while (Pos < DumpSize - 7)
                {
                    byte[] header = Bin.ReadBytes(8);
                    Pos += 8;

                    string tag = Encoding.ASCII.GetString(header, 0, 4).TrimEnd('\0');
                    int fieldType = (header[4] << 8) | header[5];
                    int dataLength = (header[6] << 8) | header[7];

                    if (Pos + dataLength > DumpSize)
                        break;

                    byte[] data = Bin.ReadBytes(dataLength);
                    Pos += dataLength;

                    Results[tag] = new ParsedField { Type = fieldType, Length = dataLength, Raw = data };
                }
            }
        }

        public void PrintParsedData()
        {
            foreach (var kv in Results)
            {
                Console.WriteLine($"{kv.Key}: Type={kv.Value.Type}, Length={kv.Value.Length}");
            }
        }

        public void PrintPlayerData(IniFile rules)
        {
            foreach (var kv in Results)
            {
                Console.WriteLine($"{kv.Key} => {BitConverter.ToString(kv.Value.Raw)}");
            }
        }
    }
}
