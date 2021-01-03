using System.IO;

namespace LePack.VFS
{
    public static class PackArchiveData
    {
        public struct Header
        {
            public const int Size = 32;

            public uint Signature;
            public uint Reserved;
            public long NumEntries;
            public long OffsetString;
            public long OffsetData;

            public void Serialize(BinaryWriter binaryWriter)
            {
                binaryWriter.Write(Signature);
                binaryWriter.Write(Reserved);
                binaryWriter.Write(NumEntries);
                binaryWriter.Write(OffsetString);
                binaryWriter.Write(OffsetData);
            }

            public void Deserialize(BinaryReader binaryReader)
            {
                Signature = binaryReader.ReadUInt32();
                Reserved = binaryReader.ReadUInt32();
                NumEntries = binaryReader.ReadInt64();
                OffsetString = binaryReader.ReadInt64();
                OffsetData = binaryReader.ReadInt64();
            }
        }

        public struct FileData
        {
            public const int Size = 28;

            public int Type;
            public int Flags;
            public int NameOffset;
            public int NameLength;
            public int DataSize;
            public long DataOffset;

            public void Serialize(BinaryWriter binaryWriter)
            {
                binaryWriter.Write(Type);
                binaryWriter.Write(Flags);
                binaryWriter.Write(NameOffset);
                binaryWriter.Write(NameLength);
                binaryWriter.Write(DataSize);
                binaryWriter.Write(DataOffset);
            }

            public void Deserialize(BinaryReader binaryReader)
            {
                Type = binaryReader.ReadInt32();
                Flags = binaryReader.ReadInt32();
                NameOffset = binaryReader.ReadInt32();
                NameLength = binaryReader.ReadInt32();
                DataSize = binaryReader.ReadInt32();
                DataOffset = binaryReader.ReadInt64();
            }

            public int NumChildren => DataSize;
            public long ChildrenOffset => DataOffset;
            public int FileSize => DataSize;
        }

        public struct Footer
        {
            public const int Size = 4;

            public uint Checksum;

            public void Serialize(BinaryWriter binaryWriter)
            {
                binaryWriter.Write(Checksum);
            }

            public void Serialize(BinaryReader binaryReader)
            {
                Checksum = binaryReader.ReadUInt32();
            }
        }

        public struct Adler32
        {
            public const uint MOD_ADLER = 65521U;

            private uint _a;
            private uint _b;

            public void Initialize()
            {
                _a = 1U;
                _b = 0U;
            }

            public void Update(byte[] bytes)
            {
                int l = bytes.Length;
                int i = 0;

                while (0 < l)
                {
                    int n = (5550 >= l) ? l : 5550;
                    l -= n;

                    do
                    {
                        _a += (uint)bytes[i];
                        _b += _a;
                        i++;
                        n--;
                    }
                    while (0 < n);

                    _a %= MOD_ADLER;
                    _b %= MOD_ADLER;
                }
            }

            public uint Finalize()
            {
                return _b << 16 | _a;
            }
        }
    }
}
